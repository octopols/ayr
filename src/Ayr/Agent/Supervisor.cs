using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;
using Ayr.App;
using Ayr.Apple;
using Ayr.Controllers;
using Ayr.Games;
using Ayr.Hardware;

namespace Ayr.Agent
{
    /// <summary>
    /// Background loop that guards what ayrctl set:
    ///  - Silent: back to Auto if CPU or GPU reaches the critical temperature, or if temperatures cannot be read.
    ///  - Turbo: GPU offsets reapplied after a driver reset, since NVAPI offsets do not survive one.
    ///  - Resume from sleep: the saved fan mode and performance level reapplied.
    ///  - Automatic Apple fast charge: one request for each Apple device per connection.
    ///  - Game mode: the game profile while one of the user's games runs, the everyday settings again once it closes.
    /// It never "corrects" a mode changed elsewhere. A sentinel process forces Auto if this one dies while Silent.
    /// </summary>
    public sealed class Supervisor : IDisposable
    {
        public const int CriticalTemperature = 90;

        private const string MutexName = "Global\\Ayr.Supervisor";
        private const string SilentLimitTestVariable = "AYR_SILENT_LIMIT";
        private static readonly TimeSpan Tick = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan ResumeSettleTime = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);
        private const int GpuCheckEveryTicks = 5;
        private const int MaxConsecutiveReadFailures = 3;
        private static readonly TimeSpan GameWatchRetryDelay = TimeSpan.FromMinutes(1);

        private readonly HardwareContext _hardware;
        private readonly ModeService _modes;
        private readonly bool _stayAliveWhenIdle;
        private readonly Action<string> _notify;
        private readonly AppleArrivalWatcher _appleWatcher = new AppleArrivalWatcher(new AppleChargeController());
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Process _sentinel;
        private int _readFailures;
        private volatile bool _resumePending;
        private GameWatcher _games;
        private string _activeGame;
        private DateTime _nextGameWatchAttempt = DateTime.MinValue;

        public Supervisor(HardwareContext hardware, bool stayAliveWhenIdle, Action<string> notify)
        {
            _hardware = hardware;
            _modes = new ModeService(hardware);
            _stayAliveWhenIdle = stayAliveWhenIdle;
            _notify = notify;
        }

        public static bool IsRunning => GlobalMutex.Exists(MutexName);

        /// <summary>Starts a hidden "ayrctl supervise" process unless a supervisor (or the tray) is already running.</summary>
        public static void EnsureBackgroundInstance()
        {
            if (IsRunning)
            {
                return;
            }
            StartDetached("supervise").Dispose();
            Stopwatch waited = Stopwatch.StartNew();
            while (!IsRunning && waited.Elapsed < StartTimeout)
            {
                Thread.Sleep(100);
            }
            if (!IsRunning)
            {
                throw new InvalidOperationException("The background supervisor did not start.");
            }
        }

        public void Run()
        {
            using (var mutex = new Mutex(true, MutexName, out bool created))
            {
                if (!created)
                {
                    return;
                }
                try
                {
                    Supervise();
                }
                finally
                {
                    // A mutex must be released by the thread that acquired it, which is this one.
                    mutex.ReleaseMutex();
                }
            }
        }

        public void Stop() => _stop.Set();

        public void Dispose()
        {
            _stop.Set();
            _sentinel?.Dispose();
            _stop.Dispose();
        }

        private void Supervise()
        {
            ActivityLog.Write("supervisor started pid " + Process.GetCurrentProcess().Id);
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            try
            {
                EnsureSentinel();
                for (int tick = 0; !_stop.WaitOne(Tick); tick++)
                {
                    EnsureSentinel();
                    DesiredState desired = DesiredState.Load();
                    if (!_stayAliveWhenIdle && !desired.NeedsSupervision)
                    {
                        break;
                    }
                    if (_resumePending)
                    {
                        _resumePending = false;
                        ReapplyAfterResume(desired);
                    }
                    TrackGames(desired);
                    GuardSilent(desired);
                    if (desired.AppleAutoFastCharge)
                    {
                        WatchApple();
                    }
                    bool turboWanted = desired.Performance == PerformanceLevel.Turbo || _activeGame != null;
                    if (turboWanted && tick % GpuCheckEveryTicks == 0)
                    {
                        KeepTurboOffsets(desired.TurboOffsets);
                    }
                }
            }
            catch (Exception ex)
            {
                ActivityLog.Write("supervisor error: " + ex);
                SafeFailsafe("supervisor error");
            }
            finally
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                StopWatchingGames();
                if (SafeReadMode() == FanMode.Silent)
                {
                    SafeFailsafe("supervisor exiting while Silent");
                }
                ActivityLog.Write("supervisor stopped");
            }
        }

        private void GuardSilent(DesiredState desired)
        {
            if (desired.Fan != FanMode.Silent || _hardware.Fans.ReadMode() != FanMode.Silent)
            {
                return;
            }
            TelemetrySample sample;
            try
            {
                sample = _hardware.Telemetry.Sample();
                _readFailures = 0;
            }
            catch (Exception ex)
            {
                if (++_readFailures >= MaxConsecutiveReadFailures)
                {
                    SafeFailsafe("cannot read temperatures while Silent: " + ex.Message);
                    Notify("Silent stopped: temperatures unreadable. Fans set to Auto.");
                }
                return;
            }
            if (sample.HottestTemperature >= SilentLimit())
            {
                SafeFailsafe("Silent guard tripped: CPU " + sample.CpuTemperature + "C GPU " + sample.GpuTemperature + "C");
                Notify("Silent stopped at CPU " + sample.CpuTemperature + "°C / GPU " + sample.GpuTemperature + "°C. Fans set to Auto.");
            }
        }

        // The end-to-end test lowers the limit through this variable to prove the guard trips without heating the
        // machine to 90 °C. It can only lower the limit, never raise it.
        private static int SilentLimit()
        {
            string raw = Environment.GetEnvironmentVariable(SilentLimitTestVariable);
            return int.TryParse(raw, out int lowered) && lowered > 0 ? Math.Min(lowered, CriticalTemperature) : CriticalTemperature;
        }

        private void KeepTurboOffsets(GpuOffsets wanted)
        {
            if (_hardware.Performance.ReadLevel() != PerformanceLevel.Turbo)
            {
                return;
            }
            try
            {
                GpuInfo gpu = _hardware.Performance.ReadGpu();
                if (!wanted.Matches(gpu))
                {
                    _hardware.Performance.ApplyGpuOffsets(wanted);
                    ActivityLog.Write("reapplied Turbo GPU offsets " + wanted + " (were " + gpu.CoreOffsetMhz + "/" + gpu.MemoryOffsetMhz + ")");
                }
            }
            catch (Exception ex)
            {
                // A driver in the middle of a reset rejects NVAPI calls; the next check retries.
                ActivityLog.Write("GPU offset check failed: " + ex.Message);
            }
        }

        private void WatchApple()
        {
            try
            {
                string sent = _appleWatcher.Poll();
                if (sent != null)
                {
                    Notify(sent);
                }
            }
            catch (Exception ex)
            {
                // Charging is a convenience; its failure must never take down the thermal guard.
                ActivityLog.Write("apple fast charge failed: " + ex.Message);
            }
        }

        // Game mode is a convenience: a failure is logged and retried later, and must never take down the thermal guard.
        private void TrackGames(DesiredState desired)
        {
            try
            {
                if (!desired.GameMode)
                {
                    DisposeGameWatcher();
                }
                else if (_games == null && DateTime.Now >= _nextGameWatchAttempt)
                {
                    _games = new GameWatcher();
                }
                string game = _games?.RunningGame();
                if (game == _activeGame)
                {
                    return;
                }
                if (game != null)
                {
                    EnterGameMode(game, desired);
                }
                else
                {
                    LeaveGameMode(desired);
                }
            }
            catch (Exception ex)
            {
                ActivityLog.Write("game mode failed: " + ex.Message);
                DisposeGameWatcher();
                _nextGameWatchAttempt = DateTime.Now + GameWatchRetryDelay;
            }
        }

        private void EnterGameMode(string game, DesiredState desired)
        {
            ApplyGameProfile(desired);
            _activeGame = game;
            ActivityLog.Write("game mode on: " + game + " (" + GameModeController.GamingPerformance + " " + desired.TurboOffsets + ", fans " + GameModeController.GamingFan + ")");
        }

        private void LeaveGameMode(DesiredState desired)
        {
            ApplyEveryday(desired);
            ActivityLog.Write("game mode off: " + _activeGame + " closed; back to fan " + desired.Fan + ", perf " + desired.Performance);
            _activeGame = null;
        }

        // Leaving the PC in the game profile after Ayr exits would quietly keep it loud and hot.
        private void StopWatchingGames()
        {
            DisposeGameWatcher();
            if (_activeGame == null)
            {
                return;
            }
            try
            {
                LeaveGameMode(DesiredState.Load());
            }
            catch (Exception ex)
            {
                ActivityLog.Write("restoring everyday settings after game mode failed: " + ex.Message);
            }
        }

        private void DisposeGameWatcher()
        {
            _games?.Dispose();
            _games = null;
        }

        private void ReapplyAfterResume(DesiredState desired)
        {
            Thread.Sleep(ResumeSettleTime);
            ActivityLog.Write("resume: reapplying " + (_activeGame != null ? "game mode for " + _activeGame : "fan=" + desired.Fan + " perf=" + desired.Performance));
            try
            {
                if (_activeGame != null)
                {
                    ApplyGameProfile(desired);
                }
                else
                {
                    ApplyEveryday(desired);
                }
            }
            catch (Exception ex)
            {
                ActivityLog.Write("resume reapply failed: " + ex.Message);
                SafeFailsafe("resume reapply failed");
            }
        }

        private void ApplyGameProfile(DesiredState desired)
        {
            _hardware.Fans.Apply(GameModeController.GamingFan);
            _hardware.Performance.Apply(GameModeController.GamingPerformance, desired.TurboOffsets);
        }

        private void ApplyEveryday(DesiredState desired)
        {
            if (desired.Fan != FanMode.Custom)
            {
                _hardware.Fans.Apply(desired.Fan);
            }
            if (desired.Performance != PerformanceLevel.Unknown)
            {
                _hardware.Performance.Apply(desired.Performance, desired.TurboOffsets);
            }
        }

        private void EnsureSentinel()
        {
            if (_sentinel != null && !_sentinel.HasExited)
            {
                return;
            }
            _sentinel?.Dispose();
            _sentinel = StartDetached("sentinel " + Process.GetCurrentProcess().Id);
        }

        // ShellExecute never inherits handles. An inheriting child kept the caller's stdout pipe open, so
        // "ayrctl fan silent | ..." never finished.
        private static Process StartDetached(string arguments) =>
            Process.Start(new ProcessStartInfo(AppPaths.ExecutablePath, arguments)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

        // An unreadable mode is treated as Silent so that the caller errs on the side of Auto.
        private FanMode SafeReadMode()
        {
            try
            {
                return _hardware.Fans.ReadMode();
            }
            catch (Exception)
            {
                return FanMode.Silent;
            }
        }

        private void SafeFailsafe(string reason)
        {
            try
            {
                _modes.FailsafeAuto(reason);
            }
            catch (Exception ex)
            {
                ActivityLog.Write("FAILSAFE FAILED: " + ex.Message);
            }
        }

        private void Notify(string message) => _notify?.Invoke(message);

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                _resumePending = true;
            }
        }
    }
}
