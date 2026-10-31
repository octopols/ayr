using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Ayr.Agent;
using Ayr.App;
using Ayr.Controllers;

namespace Ayr.Ui
{
    /// <summary>
    /// The tray host. It runs elevated and hosts the supervisor, so while it runs Silent is guarded, Turbo offsets survive
    /// driver resets and Apple devices get their fast-charge request. It has no polling of its own: windows exist only
    /// while open, and with none open the process sits in efficiency mode with its memory trimmed.
    /// </summary>
    public sealed class TrayApp : ApplicationContext
    {
        private const string AppName = "Ayr";
        private const int TooltipLimit = 63;
        private const int BalloonMs = 5000;
        private static readonly TimeSpan TooltipMinInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SupervisorExitTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan StartupSettleTime = TimeSpan.FromSeconds(20);
        // Pressing the tray icon while the panel is open first takes focus away, which closes the panel; the click that
        // follows within this window must not reopen it.
        private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(400);

        private readonly HardwareContext _hardware = new HardwareContext();
        private readonly UiBackend _backend;
        private readonly Icon _trayIcon = Brand.LoadTrayIcon();
        private readonly NotifyIcon _notifyIcon;
        private readonly Control _uiThread = new Control();
        private readonly Supervisor _supervisor;
        private readonly Thread _supervisorThread;
        private readonly AutoUpdateTimer _autoUpdate;
        private MainWindow _main;
        private QuickPanel _quick;
        private TrayAnchor _anchor;
        private DateTime _lastTooltip = DateTime.MinValue;
        private DateTime _quickClickedAway = DateTime.MinValue;

        /// <param name="startupNotice">Shown as a notification once the tray is up, or null.</param>
        public TrayApp(string startupNotice)
        {
            // Background results are marshalled through this control, so it needs its window handle before any window opens.
            _ = _uiThread.Handle;
            _backend = new UiBackend(_hardware);
            _backend.ModeChanged += change => _uiThread.BeginInvoke((Action)(() => OnModeChanged(change)));
            _backend.UpdateInstalled += () => _uiThread.BeginInvoke((Action)RestartIntoNewVersion);
            _autoUpdate = new AutoUpdateTimer(_backend, _uiThread, canRestart: () => !AnyWindowOpen);

            _notifyIcon = new NotifyIcon { Icon = _trayIcon, Text = AppName, Visible = true };
            if (startupNotice != null)
            {
                _notifyIcon.ShowBalloonTip(BalloonMs, AppName, startupNotice, ToolTipIcon.Info);
            }
            _notifyIcon.MouseDown += (sender, e) => _anchor = TrayAnchor.Capture(Cursor.Position);
            _notifyIcon.MouseClick += (sender, e) => ToggleQuickPanel();
            _notifyIcon.MouseDoubleClick += (sender, e) => OpenMainWindow();
            _notifyIcon.MouseMove += (sender, e) => RefreshTooltip();

            _supervisor = new Supervisor(_hardware, stayAliveWhenIdle: true, notify: ShowWarning);
            // The thread carries the Silent thermal guard, which must stay prompt even in efficiency mode.
            _supervisorThread = new Thread(_supervisor.Run) { IsBackground = true, Name = "supervisor", Priority = ThreadPriority.AboveNormal };
            _supervisorThread.Start();

            EfficiencyMode.Enter();
            TrimOnceStartupSettles();
        }

        private bool AnyWindowOpen => _main != null || _quick != null;

        protected override void ExitThreadCore()
        {
            _autoUpdate.Dispose();
            _main?.Close();
            _quick?.Close();
            _supervisor.Stop();
            // The supervisor switches Silent to Auto on its way out; that must finish before the process ends.
            if (_supervisorThread.Join(SupervisorExitTimeout))
            {
                _supervisor.Dispose();
            }
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
            _uiThread.Dispose();
            base.ExitThreadCore();
        }

        // Stopping the supervisor switches Silent to Auto and saves that; the new version should start from the user's choice.
        private void RestartIntoNewVersion()
        {
            DesiredState chosen = DesiredState.Load();
            ExitThread();
            chosen.Save();
            TrayLauncher.StartAfterUpdate();
        }

        private void TrimOnceStartupSettles()
        {
            var settle = new System.Windows.Forms.Timer { Interval = (int)StartupSettleTime.TotalMilliseconds };
            settle.Tick += (sender, e) =>
            {
                settle.Dispose();
                if (!AnyWindowOpen)
                {
                    EfficiencyMode.Enter();
                }
            };
            settle.Start();
        }

        private TrayAnchor CurrentAnchor() => _anchor ?? TrayAnchor.Capture(Cursor.Position);

        private void OpenMainWindow()
        {
            _quick?.Close();
            if (_main != null)
            {
                _main.WindowState = FormWindowState.Normal;
                _main.Activate();
                return;
            }
            EfficiencyMode.Leave();
            _main = new MainWindow(_backend);
            _main.PlaceNear(CurrentAnchor());
            _main.FormClosed += (sender, e) =>
            {
                _main = null;
                if (!AnyWindowOpen)
                {
                    EfficiencyMode.Enter();
                }
            };
            _main.Show();
            _main.Activate();
        }

        private void ToggleQuickPanel()
        {
            if (_quick != null)
            {
                _quick.Close();
                return;
            }
            if (DateTime.Now - _quickClickedAway < ReopenGuard)
            {
                return;
            }
            EfficiencyMode.Leave();
            _quick = new QuickPanel(_backend);
            _quick.PlaceNear(CurrentAnchor());
            _quick.OpenRequested += OpenMainWindow;
            _quick.ExitRequested += ExitThread;
            _quick.FormClosed += (sender, e) =>
            {
                if (((QuickPanel)sender).ClosedByClickAway)
                {
                    _quickClickedAway = DateTime.Now;
                }
                _quick = null;
                if (!AnyWindowOpen)
                {
                    EfficiencyMode.Enter();
                }
            };
            _quick.Show();
        }

        // The on-screen display is for changes made with no window in view (later the Fn key); in a window the change is visible.
        private void OnModeChanged(ModeChange change)
        {
            bool windowInView = _quick != null || (_main != null && _main.WindowState != FormWindowState.Minimized);
            if (!windowInView)
            {
                Osd.Show(change);
            }
        }

        private void RefreshTooltip()
        {
            if (DateTime.Now - _lastTooltip < TooltipMinInterval)
            {
                return;
            }
            _lastTooltip = DateTime.Now;
            _backend.Run(_uiThread, _backend.ReadLive, (live, error) =>
            {
                string text = error != null
                    ? AppName
                    : AppName + "\nFan " + live.Fan + " · " + live.Performance
                        + "\nCPU " + live.Telemetry.CpuTemperature + "°C · GPU " + live.Telemetry.GpuTemperature + "°C";
                _notifyIcon.Text = text.Length > TooltipLimit ? text.Substring(0, TooltipLimit) : text;
            });
        }

        private void ShowWarning(string message) =>
            _uiThread.BeginInvoke((Action)(() => _notifyIcon.ShowBalloonTip(BalloonMs, AppName, message, ToolTipIcon.Warning)));
    }
}
