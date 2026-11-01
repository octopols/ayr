using System;
using System.Threading;
using System.Windows.Forms;
using Ayr.Agent;
using Ayr.App;
using Ayr.Apple;
using Ayr.Controllers;
using Ayr.Games;
using Ayr.Hardware;
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

        public UiBackend(HardwareContext hardware)
        {
            _hardware = hardware;
            _modes = new ModeService(hardware);
        }

        public event Action<ModeChange> ModeChanged;

        /// <summary>Raised on a pool thread once a new version is in place; the tray then restarts into it.</summary>
        public event Action UpdateInstalled;

        /// <summary>Runs hardware work off the UI thread and delivers its result, or its error, on the owner's thread.</summary>
        public void Run<T>(Control owner, Func<T> work, Action<T, Exception> done) => RunOnPool(owner, work, done, _oneCallAtATime);

        /// <summary>Like Run for update work, which only waits on the network and must not hold up hardware reads.</summary>
        public void RunUpdate<T>(Control owner, Func<T> work, Action<T, Exception> done) => RunOnPool(owner, work, done, _oneUpdateAtATime);

        public LiveState ReadLive() => new LiveState(
            _hardware.Fans.ReadMode(),
            _hardware.Performance.ReadLevel(),
            _hardware.Telemetry.Sample(),
            ReadIfAvailable(() => _hardware.KeyboardLight.IsSupported, _hardware.KeyboardLight.ReadLevel));

        public SlowState ReadSlow(bool includeAutostart) => new SlowState(
            DesiredState.Load(),
            new CurveTemperatures(_hardware.Fans.ReadPointTemperatures(FanKind.Cpu), _hardware.Fans.ReadPointTemperatures(FanKind.Gpu)),
            ReadIfAvailable(() => _hardware.ChargeLimit.IsSupported, _hardware.ChargeLimit.ReadPercent),
            _games.Overview(),
            _charger.ConnectedStatuses(),
            _charger.NextRequestAllowedAt,
            includeAutostart ? _autostart.IsInstalled : (bool?)null,
            MsiSoftware.IsMsiCenterRunning);

        public AppleSummary ReadAppleSummary() => new AppleSummary(DesiredState.Load().AppleAutoFastCharge, _charger.ConnectedStatuses());

        public string ApplyFan(FanMode mode)
        {
            _modes.ApplyFan(mode);
            if (mode == FanMode.Silent)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            ModeChanged?.Invoke(new ModeChange(mode + " mode", "Fan profile updated", OsdIcon.Fan));
            return mode + " mode applied";
        }

        public string ApplyCustomCurves(FanCurves curves)
        {
            _modes.ApplyCustomCurves(curves);
            return "Custom curve applied";
        }

        public string ApplyChargeLimit(int percent)
        {
            _modes.ApplyChargeLimit(percent);
            return percent == ChargeLimitController.Full ? "Charging to full" : "Charging stops at " + percent + "%";
        }

        public string ApplyKeyboardLight(int level)
        {
            _modes.ApplyKeyboardLight(level);
            return "Keyboard light " + KeyboardLightController.LevelNames[level].ToLowerInvariant();
        }

        public string ApplyPerformance(PerformanceLevel level)
        {
            _modes.ApplyPerformance(level);
            bool turbo = level == PerformanceLevel.Turbo;
            if (turbo)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            string detail = turbo ? "GPU " + DesiredState.Load().TurboOffsets : "GPU offsets off";
            ModeChanged?.Invoke(new ModeChange(level + " performance", detail, OsdIcon.Performance));
            return level + " performance applied";
        }

        public string ApplyTurboOffsets(GpuOffsets offsets)
        {
            bool applied = _modes.SetTurboOffsets(offsets);
            if (applied)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            return applied ? "GPU offsets applied: " + offsets : "GPU offsets saved for the next Turbo session";
        }

        public string SetGameMode(bool on)
        {
            _games.SetEnabled(on);
            if (on)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            return on ? "Game mode on" : "Game mode off";
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
            return found == 1 ? "1 game found on this PC" : found + " games found on this PC";
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
                return "Automatic fast charge off";
            }
            Supervisor.EnsureBackgroundInstance();
            ChargeRequestResult sentNow = _charger.SendIfAnyReady();
            return sentNow != null && sentNow.Sent ? sentNow.Message : "Automatic fast charge on";
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
                return "Settings will be applied at login";
            }
            _autostart.Remove();
            return "Login task removed";
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
    }
}
