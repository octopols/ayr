using System;
using System.Threading;
using System.Windows.Forms;
using Ayr.Agent;
using Ayr.App;
using Ayr.Apple;
using Ayr.Controllers;

namespace Ayr.Ui
{
    /// <summary>
    /// The windows' only path to the hardware. Work runs on a pool thread so windows never freeze, one call at a time
    /// because the supervisor thread in this process uses the same hardware.
    /// </summary>
    public sealed class UiBackend
    {
        private readonly HardwareContext _hardware;
        private readonly ModeService _modes;
        private readonly AppleChargeController _charger = new AppleChargeController();
        private readonly AutostartTask _autostart = new AutostartTask();
        private readonly object _oneCallAtATime = new object();

        public UiBackend(HardwareContext hardware)
        {
            _hardware = hardware;
            _modes = new ModeService(hardware);
        }

        public event Action<ModeChange> ModeChanged;

        /// <summary>Runs work off the UI thread and delivers its result, or its error, on the owner's thread.</summary>
        public void Run<T>(Control owner, Func<T> work, Action<T, Exception> done)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                T result = default(T);
                Exception error = null;
                try
                {
                    lock (_oneCallAtATime)
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

        public LiveState ReadLive() =>
            new LiveState(_hardware.Fans.ReadMode(), _hardware.Performance.ReadLevel(), _hardware.Telemetry.Sample());

        public SlowState ReadSlow(bool includeAutostart) => new SlowState(
            DesiredState.Load(),
            _charger.Driver.IsAvailable,
            _charger.Inventory.List(includeDisconnected: false),
            includeAutostart ? _autostart.IsInstalled : (bool?)null);

        public AppleSummary ReadAppleSummary() =>
            new AppleSummary(DesiredState.Load().AppleFastCharge, _charger.Inventory.ConnectedInstanceIds().Count);

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

        public string SetAppleFastCharge(bool on)
        {
            _charger.SetAutomatic(on);
            if (on)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            return on ? "Apple fast charge on" : "Apple fast charge off";
        }

        public string SendAppleFastChargeNow() => _charger.SendForConnected().Message;

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
