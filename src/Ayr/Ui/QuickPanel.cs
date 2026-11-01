using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Ui.Controls;

namespace Ayr.Ui
{
    /// <summary>
    /// The tray popup: fan mode, performance level and Apple fast charge. It reads state once when opened, never polls,
    /// and closes once the user clicks somewhere else.
    /// </summary>
    public sealed class QuickPanel : PopupWindow
    {
        private const int BaseWidth = 320;
        private const int EdgePadding = 16;
        private const string ExitLabel = "Exit";
        private const string ExitAnywayLabel = "Exit anyway";

        private readonly UiBackend _backend;
        private readonly MutedText _status;
        private readonly FlatButton _exit;
        private readonly Segmented _fan;
        private readonly Segmented _performance;
        private readonly TitleWithSubtitle _appleTitle;
        private readonly ToggleSwitch _appleToggle;
        private bool _busy;

        public QuickPanel(UiBackend backend)
        {
            _backend = backend;
            ShowInTaskbar = false;
            TopMost = true;

            int width = Scaled(BaseWidth);
            int left = Scaled(EdgePadding);
            int inner = width - 2 * left;
            int y = Scaled(EdgePadding);

            AddAt(new BrandBadge(Theme), left, y, Scaled(28), Scaled(28));
            AddAt(new TitleWithSubtitle(Theme, "Ayr", null, Theme.Section, Theme.Background), left + Scaled(36), y + Scaled(4), inner - Scaled(36), Scaled(24));
            y += Scaled(34);
            _status = AddAt(new MutedText(Theme, "Reading…", Theme.Background), left, y, inner, Scaled(18));
            y += Scaled(28);

            AddAt(new MutedText(Theme, "Performance", Theme.Background), left, y, inner, Scaled(18));
            y += Scaled(20);
            _performance = AddAt(new Segmented(Theme, PerformanceController.SelectableLevels.Select(level => level.ToString()).ToList())
            {
                AccessibleName = "Performance"
            }, left, y, inner, Scaled(36));
            _performance.Picked += index => Apply(() => _backend.ApplyPerformance(PerformanceController.SelectableLevels[index]));
            y += Scaled(46);

            AddAt(new MutedText(Theme, "Fans", Theme.Background), left, y, inner, Scaled(18));
            y += Scaled(20);
            _fan = AddAt(new Segmented(Theme, FanController.SelectableModes.Select(mode => mode.ToString()).ToList()) { AccessibleName = "Fans" },
                left, y, inner, Scaled(36));
            _fan.Picked += index => Apply(() => _backend.ApplyFan(FanController.SelectableModes[index]));
            y += Scaled(46);

            var appleCard = AddAt(new Card(Theme), left, y, inner, Scaled(56));
            _appleTitle = new TitleWithSubtitle(Theme, "iPhone & iPad fast charge", "", Theme.BodyBold, Theme.Surface);
            _appleTitle.SetBounds(Scaled(12), Scaled(10), inner - Scaled(70), Scaled(38));
            _appleToggle = new ToggleSwitch(Theme) { BackColor = Theme.Surface, AccessibleName = "iPhone & iPad fast charge" };
            _appleToggle.SetBounds(inner - Scaled(52), Scaled(17), Scaled(40), Scaled(22));
            _appleToggle.Toggled += on => Apply(() => _backend.SetAppleAutoFastCharge(on));
            appleCard.Controls.Add(_appleTitle);
            appleCard.Controls.Add(_appleToggle);
            y += Scaled(68);

            AddAt(new Panel { BackColor = Theme.Border }, left, y, inner, 1);
            y += Scaled(12);
            int buttonWidth = (inner - Scaled(8)) / 2;
            var open = AddAt(new FlatButton(Theme, "Open Ayr", ButtonStyle.Primary), left, y, buttonWidth, Scaled(36));
            open.Click += (sender, e) => CloseThen(OpenRequested);
            _exit = AddAt(new FlatButton(Theme, ExitLabel, ButtonStyle.Secondary), left + (inner + Scaled(8)) / 2, y, buttonWidth, Scaled(36));
            _exit.Click += (sender, e) => OnExitClicked();
            y += Scaled(36 + EdgePadding);

            ClientSize = new Size(width, y);
        }

        public event Action OpenRequested;
        public event Action ExitRequested;

        /// <summary>The off-screen preview turns this off so the panel stays open long enough to be captured.</summary>
        public bool CloseOnDeactivate { get; set; } = true;

        /// <summary>Set when the panel closed because the user clicked somewhere else.</summary>
        public bool ClosedByClickAway { get; private set; }

        protected override int ExtendedWindowStyle => WindowStyles.ToolWindow;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Reload();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (_busy || !CloseOnDeactivate)
            {
                return;
            }
            // Like the shell's own flyouts, close only when the user has gone elsewhere. Losing focus while the pointer
            // is still on the panel means another window popped up, so focus is taken back instead.
            if (Bounds.Contains(Cursor.Position))
            {
                BeginInvoke((Action)Activate);
                return;
            }
            ClosedByClickAway = true;
            Close();
        }

        // After a change the status line keeps saying what the change did instead of going back to temperatures.
        private void Reload(bool keepStatus = false)
        {
            _backend.Run(this, _backend.ReadLive, (live, error) =>
            {
                if (error != null)
                {
                    _status.Text = "Can't read fans and performance. Open Ayr for details.";
                    return;
                }
                _fan.SelectedIndex = FanController.SelectableModes.IndexOf(live.Fan);
                _performance.SelectedIndex = PerformanceController.SelectableLevels.IndexOf(live.Performance);
                if (!keepStatus)
                {
                    _status.Text = "CPU " + live.Telemetry.CpuTemperature + "°C · GPU " + live.Telemetry.GpuTemperature + "°C";
                }
            });
            _backend.Run(this, _backend.ReadAppleSummary, (summary, error) =>
            {
                if (error == null)
                {
                    _appleToggle.Checked = summary.Automatic;
                    _appleTitle.SetSubtitle(summary.Description);
                }
            });
        }

        private void Apply(Func<string> change)
        {
            if (_busy)
            {
                return;
            }
            _busy = true;
            _status.Text = "Applying…";
            _backend.Run(this, change, (message, error) =>
            {
                _busy = false;
                _status.Text = error == null ? message : "Couldn't change that: " + ExceptionText.Of(error);
                Reload(keepStatus: true);
            });
        }

        private void OnExitClicked()
        {
            string interrupted = _backend.WorkAnExitWouldStop();
            if (interrupted == null || _exit.Text == ExitAnywayLabel)
            {
                CloseThen(ExitRequested);
                return;
            }
            _status.Text = interrupted + " Exiting Ayr stops it.";
            _exit.Text = ExitAnywayLabel;
        }

        private void CloseThen(Action next)
        {
            Close();
            next?.Invoke();
        }

        private T AddAt<T>(T control, int x, int y, int width, int height) where T : Control
        {
            control.SetBounds(x, y, width, height);
            Controls.Add(control);
            return control;
        }
    }
}
