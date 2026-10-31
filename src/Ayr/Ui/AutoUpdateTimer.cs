using System;
using System.Windows.Forms;
using Ayr.App;

namespace Ayr.Ui
{
    /// <summary>
    /// While automatic updates are on, checks GitHub for a new release once a day and installs it. Installing restarts the
    /// tray, so it waits until no Ayr window is open.
    /// </summary>
    public sealed class AutoUpdateTimer : IDisposable
    {
        // At sign-in the network is often not up yet.
        private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);
        private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

        private readonly UiBackend _backend;
        private readonly Control _uiThread;
        private readonly Func<bool> _canRestart;
        private readonly Timer _timer = new Timer { Interval = (int)FirstCheckDelay.TotalMilliseconds };
        private DateTime _lastCheck = DateTime.MinValue;
        private bool _inFlight;

        public AutoUpdateTimer(UiBackend backend, Control uiThread, Func<bool> canRestart)
        {
            _backend = backend;
            _uiThread = uiThread;
            _canRestart = canRestart;
            _timer.Tick += (sender, e) => OnTick();
            _timer.Start();
        }

        public void Dispose() => _timer.Dispose();

        private void OnTick()
        {
            _timer.Interval = (int)TickInterval.TotalMilliseconds;
            bool due = DateTime.Now - _lastCheck >= CheckInterval;
            if (_inFlight || !due || !_canRestart() || !DesiredState.Load().AutoUpdate)
            {
                return;
            }
            _inFlight = true;
            _backend.RunUpdate(_uiThread, _backend.InstallUpdateIfAvailable, (installed, error) =>
            {
                _inFlight = false;
                if (error != null)
                {
                    // Offline or GitHub unreachable: the next tick tries again.
                    ActivityLog.Write("automatic update check failed: " + error.Message);
                    return;
                }
                _lastCheck = DateTime.Now;
            });
        }
    }
}
