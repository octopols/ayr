using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Forms;
using Ayr.Agent;
using Ayr.App;
using Ayr.Apple;
using Ayr.Battery;
using Ayr.Controllers;
using Ayr.Games;
using Ayr.Hardware;
using Ayr.MsiRemoval;
using Ayr.Updates;

namespace Ayr.Ui
{
    /// <summary>
    /// The windows' only path to the hardware and to updates. Work runs on a pool thread so windows never freeze; hardware
    /// work runs one call at a time because the supervisor thread in this process uses the same hardware.
    /// </summary>
    public sealed class UiBackend
    {
        private readonly HardwareContext _hardware;
        private readonly ModeService _modes;
        private readonly AppleChargeController _charger = new AppleChargeController();
        private readonly AutostartTask _autostart = new AutostartTask();
        private readonly GameModeController _games = new GameModeController();
        private readonly GitHubReleaseFeed _releases = new GitHubReleaseFeed();
        private readonly UpdateInstaller _installer = new UpdateInstaller();
        private readonly object _oneCallAtATime = new object();
        private readonly object _oneUpdateAtATime = new object();
        private readonly object _oneMsiTaskAtATime = new object();
        private readonly object _oneBatteryReportAtATime = new object();
        private readonly Lazy<bool> _calibrationAvailable;
        private readonly Lazy<bool> _gpuOverclockAvailable;
        private int _removalsRunning;

        public UiBackend(HardwareContext hardware)
        {
            _hardware = hardware;
            _modes = new ModeService(hardware);
            _calibrationAvailable = new Lazy<bool>(() => hardware.Charger.IsAvailable);
            _gpuOverclockAvailable = new Lazy<bool>(() => hardware.Performance.TryReadGpu().HasValue);
        }

        public event Action<ModeChange> ModeChanged;

        /// <summary>Raised on a pool thread once a new version is in place; the tray then restarts into it.</summary>
        public event Action UpdateInstalled;

        /// <summary>Runs hardware work off the UI thread and delivers its result, or its error, on the owner's thread.</summary>
        public void Run<T>(Control owner, Func<T> work, Action<T, Exception> done) => RunOnPool(owner, work, done, _oneCallAtATime);

        /// <summary>Like Run for update work, which only waits on the network and must not hold up hardware reads.</summary>
        public void RunUpdate<T>(Control owner, Func<T> work, Action<T, Exception> done) => RunOnPool(owner, work, done, _oneUpdateAtATime);

        /// <summary>Uninstallers can take minutes, so MSI scans and removals have their own queue and never hold up hardware reads.</summary>
        public void ScanMsiSoftware(Control owner, Action<MsiScanResult, Exception> done) =>
            RunOnPool(owner, () => new MsiSoftwareScan().Run(), done, _oneMsiTaskAtATime);

        /// <param name="progress">Called on the owner's thread.</param>
        public void RemoveMsiSoftware(Control owner, IReadOnlyList<MsiComponent> components, bool makeRestorePoint, IRemovalProgress progress,
            Action<RemovalReport, Exception> done) =>
            RunOnPool(owner, () => RemoveCounted(components, makeRestorePoint, new ProgressOnThread(owner, progress)), done, _oneMsiTaskAtATime);

        /// <summary>The removal runs in this process whether or not its window stays open.</summary>
        public bool RemovingMsiSoftware => Volatile.Read(ref _removalsRunning) > 0;

        /// <summary>A sentence saying what restarting or closing Ayr now would cut short; null when nothing would.</summary>
        public string WorkAnExitWouldStop()
        {
            if (RemovingMsiSoftware)
            {
                return "MSI software is still being removed.";
            }
            return BatteryCalibration.InProgress ? "A battery calibration is running." : null;
        }

        /// <summary>
        /// Has Windows format its battery report from the usage data it records anyway. Only the Battery page asks for it,
        /// on its own queue so it never holds up hardware reads.
        /// </summary>
        public void ReadBatteryHistory(Control owner, Action<BatteryHistory, Exception> done) =>
            RunOnPool(owner, BatteryHistory.Read, done, _oneBatteryReportAtATime);

        public LiveState ReadLive() => new LiveState(
            _hardware.Fans.ReadMode(),
            _hardware.Performance.ReadLevel(),
            _hardware.Telemetry.Sample(),
            ReadIfAvailable(() => _hardware.KeyboardLight.IsSupported, _hardware.KeyboardLight.ReadLevel));

        public SlowState ReadSlow(bool includeAutostart)
        {
            DesiredState desired = DesiredState.Load();
            BatteryReading battery = ReadBattery();
            return new SlowState(
                desired,
                new CurveTemperatures(_hardware.Fans.ReadPointTemperatures(FanKind.Cpu), _hardware.Fans.ReadPointTemperatures(FanKind.Gpu)),
                ReadIfAvailable(() => _hardware.ChargeLimit.IsSupported, _hardware.ChargeLimit.ReadPercent),
                _games.Overview(),
                _charger.ConnectedStatuses(),
                _charger.NextRequestAllowedAt,
                includeAutostart ? _autostart.IsInstalled : (bool?)null,
                MsiSoftware.IsMsiCenterRunning,
                new FnKeyBindings(desired, MsiSoftware.UsesFnF7),
                ReadBatteryHealth(),
                battery,
                _calibrationAvailable.Value ? new CalibrationView(desired.CalibrationWanted, CalibrationState.Load(), battery) : null,
                _gpuOverclockAvailable.Value);
        }

        public AppleSummary ReadAppleSummary() => new AppleSummary(DesiredState.Load().AppleAutoFastCharge, _charger.ConnectedStatuses());

        public string ApplyFan(FanMode mode)
        {
            _modes.ApplyFan(mode);
            if (mode == FanMode.Silent)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            ModeChanged?.Invoke(new ModeChange(mode.ToString(), "Fans", OsdIcon.Fan));
            return "Fans set to " + mode;
        }

        public string ApplyCustomCurves(FanCurves curves)
        {
            _modes.ApplyCustomCurves(curves);
            return "Fans now follow your custom curve";
        }

        public string ApplyChargeLimit(int percent)
        {
            if (BatteryCalibration.InProgress)
            {
                throw new InvalidOperationException("The battery calibration holds the charge limit at Full. Cancel it or let it finish first.");
            }
            _modes.ApplyChargeLimit(percent);
            return percent == ChargeLimitController.Full ? "The battery now charges to full" : "Charging now stops at " + percent + "%";
        }

        public string ApplyKeyboardLight(int level)
        {
            _modes.ApplyKeyboardLight(level);
            return "Keyboard light set to " + KeyboardLightController.LevelNames[level];
        }

        public string ApplyPerformance(PerformanceLevel level)
        {
            _modes.ApplyPerformance(level);
            bool turbo = level == PerformanceLevel.Turbo;
            if (turbo)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            bool overclocked = turbo && DesiredState.Load().TurboOffsets.RaisesClocks;
            string area = overclocked ? "Performance, with GPU overclock" : "Performance";
            ModeChanged?.Invoke(new ModeChange(level.ToString(), area, OsdIcon.Performance));
            return "Performance set to " + level;
        }

        public string StepPerformance() =>
            ApplyPerformance(NextAfter(PerformanceController.SelectableLevels, _hardware.Performance.ReadLevel()));

        public string StepFanMode() => ApplyFan(NextAfter(FanController.SelectableModes, _hardware.Fans.ReadMode()));

        public string SetFnKeyAction(FnKey key, FnKeyAction action)
        {
            DesiredState.Update(state => state.ChooseFnAction(key, action));
            ActivityLog.Write("Fn+" + key + " -> " + action);
            return "Fn+" + key + " now " + FnKeyText.Effect(action);
        }

        public string ApplyTurboOffsets(GpuOffsets offsets)
        {
            bool applied = _modes.SetTurboOffsets(offsets);
            if (applied)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            return applied ? "GPU overclock saved and applied" : "GPU overclock saved. It's added whenever Turbo is on";
        }

        public string SetGameMode(bool on)
        {
            int added = _games.SetEnabled(on);
            if (!on)
            {
                return "Game mode off";
            }
            Supervisor.EnsureBackgroundInstance();
            return added == 0 ? "Game mode on"
                : added == 1 ? "Game mode on. Added the game found on this PC"
                : "Game mode on. Added the " + added + " games found on this PC";
        }

        // The background process starts first: a request it never picks up would hold the charge limit for nothing.
        public string StartCalibration()
        {
            Supervisor.EnsureBackgroundInstance();
            DesiredState.Update(state => state.CalibrationWanted = true);
            return "Battery calibration started. Keep the charger plugged in";
        }

        public string CancelCalibration()
        {
            DesiredState.Update(state => state.CalibrationWanted = false);
            return "Battery calibration cancelled";
        }

        public string SetGameProfile(GameProfile profile)
        {
            _games.SetProfile(profile);
            return "Game settings saved";
        }

        public string AddGame(GameEntry game)
        {
            _games.Add(game);
            return game.Name + " added";
        }

        public string AddGamePath(string path) => _games.AddPath(path).Name + " added";

        public string ScanForGames()
        {
            int found = _games.ScanAgain();
            return found == 0 ? "No new games found" : found == 1 ? "Found 1 new game" : "Found " + found + " new games";
        }

        public string RemoveGame(GameEntry game)
        {
            _games.Remove(game);
            return game.Name + " removed";
        }

        /// <summary>Switching it on also sends to devices already connected, rather than waiting for the next plug-in.</summary>
        public string SetAppleAutoFastCharge(bool on)
        {
            _charger.SetAutomatic(on);
            if (!on)
            {
                return "iPhones and iPads no longer fast charge automatically";
            }
            Supervisor.EnsureBackgroundInstance();
            ChargeRequestResult sentNow = _charger.SendIfAnyReady();
            return sentNow != null && sentNow.Sent ? sentNow.Message : "iPhones and iPads now fast charge automatically";
        }

        public string SendAppleFastCharge()
        {
            ChargeRequestResult result = _charger.SendForConnected();
            if (!result.Sent)
            {
                throw new InvalidOperationException(result.Message);
            }
            return result.Message;
        }

        public string SetAutostart(bool on)
        {
            if (on)
            {
                _autostart.Install();
                return "Ayr now starts with Windows";
            }
            _autostart.Remove();
            return "Ayr no longer starts with Windows";
        }

        public string SetAutoUpdate(bool on)
        {
            DesiredState.Update(state => state.AutoUpdate = on);
            ActivityLog.Write("automatic updates " + (on ? "on" : "off"));
            return on ? "Automatic updates on" : "Automatic updates off";
        }

        /// <summary>The newer release, or null when this is the latest.</summary>
        public ReleaseInfo FindUpdate() => _releases.FindNewerThan(AppVersion.Current);

        public string InstallUpdate(ReleaseInfo release)
        {
            string interrupted = WorkAnExitWouldStop();
            if (interrupted != null)
            {
                throw new InvalidOperationException(interrupted + " Updating restarts Ayr and would stop it, so update when it's done.");
            }
            _installer.Install(release);
            UpdateInstalled?.Invoke();
            return "Updated to " + release.Version;
        }

        /// <summary>Installs the latest release if it is newer; returns its version, or null when already up to date.</summary>
        public Version InstallUpdateIfAvailable()
        {
            ReleaseInfo release = FindUpdate();
            if (release == null)
            {
                return null;
            }
            InstallUpdate(release);
            return release.Version;
        }

        /// <summary>Wraps round to the first; a current value that is not in the list (such as Unknown) also gives the first.</summary>
        private static T NextAfter<T>(ReadOnlyCollection<T> choices, T current) => choices[(choices.IndexOf(current) + 1) % choices.Count];

        private BatteryHealth ReadBatteryHealth()
        {
            try
            {
                return BatteryHealth.Read(_hardware.Wmi);
            }
            catch (Exception ex) when (BatteryHealth.IsReadFailure(ex))
            {
                return null;
            }
        }

        private static BatteryReading ReadBattery()
        {
            try
            {
                return BatteryReading.Now();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        // Charge limit and keyboard light live only in EC RAM, at addresses known for some firmware only. Where either isn't
        // the case, their controls are simply hidden.
        private static int? ReadIfAvailable(Func<bool> isSupported, Func<int> read)
        {
            try
            {
                return isSupported() ? read() : (int?)null;
            }
            catch (EcUnavailableException)
            {
                return null;
            }
        }

        private RemovalReport RemoveCounted(IReadOnlyList<MsiComponent> components, bool makeRestorePoint, IRemovalProgress progress)
        {
            Interlocked.Increment(ref _removalsRunning);
            try
            {
                return new MsiRemover().Remove(components, makeRestorePoint, progress);
            }
            finally
            {
                Interlocked.Decrement(ref _removalsRunning);
            }
        }

        private static void RunOnPool<T>(Control owner, Func<T> work, Action<T, Exception> done, object exclusive)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                T result = default(T);
                Exception error = null;
                try
                {
                    lock (exclusive)
                    {
                        result = work();
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                Deliver(owner, () => done(result, error));
            });
        }

        private static void Deliver(Control owner, Action callback)
        {
            if (owner.IsDisposed || !owner.IsHandleCreated)
            {
                return;
            }
            try
            {
                owner.BeginInvoke(callback);
            }
            catch (InvalidOperationException)
            {
                // The window closed while the work ran; there is nothing left to update.
            }
        }

        private sealed class ProgressOnThread : IRemovalProgress
        {
            private readonly Control _owner;
            private readonly IRemovalProgress _progress;

            public ProgressOnThread(Control owner, IRemovalProgress progress)
            {
                _owner = owner;
                _progress = progress;
            }

            public void Starting(MsiComponent component) => Deliver(_owner, () => _progress.Starting(component));

            public void Finished(RemovalResult result) => Deliver(_owner, () => _progress.Finished(result));
        }
    }
}
