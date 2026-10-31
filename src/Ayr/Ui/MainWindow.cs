using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ayr.Apple;
using Ayr.Controllers;
using Ayr.Hardware;
using Ayr.Ui.Controls;

namespace Ayr.Ui
{
    /// <summary>
    /// The full window. It exists only while open, and its timers run only while it is visible, so the tray process stays
    /// near idle the rest of the time.
    /// </summary>
    public sealed class MainWindow : PopupWindow
    {
        private const int BaseWidth = 460;
        private const int HeaderHeight = 64;
        private const int EdgePadding = 16;
        private const int CardPadding = 14;
        private const int ScreenMargin = 40;
        private const int MaxDeviceRows = 3;
        private const string ReadErrorPrefix = "Couldn't read the hardware: ";
        private const string MsiCenterWarning = "MSI Center is running and overrides Ayr's settings after sleep. Uninstall MSI Center.";
        private static readonly TimeSpan LiveInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SlowInterval = TimeSpan.FromSeconds(15);

        private readonly UiBackend _backend;
        private readonly Icon _windowIcon = Brand.LoadIcon(new Size(32, 32));
        private readonly Timer _liveTimer = new Timer { Interval = (int)LiveInterval.TotalMilliseconds };
        private readonly Timer _slowTimer = new Timer { Interval = (int)SlowInterval.TotalMilliseconds };

        private readonly HeaderBand _header;
        private readonly ScrollViewport _viewport;
        private readonly Panel _page;
        private readonly ErrorBar _error;
        private readonly TitleWithSubtitle _statusTitle;
        private readonly Card _statusCard;
        private readonly MetricCell[] _metrics;
        private readonly MetricCell _cpuTemperature, _gpuTemperature, _cpuFan, _gpuFan;
        private readonly TitleWithSubtitle _fanTitle;
        private readonly Segmented _fan;
        private readonly MutedText _fanNote;
        private readonly TitleWithSubtitle _performanceTitle;
        private readonly Segmented _performance;
        private readonly Card _overclockCard;
        private readonly TitleWithSubtitle _overclockTitle;
        private readonly LabeledSlider _coreOffset, _memoryOffset;
        private readonly FlatButton _applyOverclock;
        private readonly TitleWithSubtitle _appleTitle;
        private readonly ToggleSwitch _appleToggle;
        private readonly Card _appleCard;
        private readonly List<DeviceRow> _deviceRows = new List<DeviceRow>();
        private readonly FlatButton _chargeNow;
        private readonly TitleWithSubtitle _settingsTitle;
        private readonly Card _settingsCard;
        private readonly TitleWithSubtitle _autostartRow;
        private readonly ToggleSwitch _autostartToggle;

        private bool _slidersInitialized;
        private bool _liveInFlight, _slowInFlight, _busy;
        private string _errorMessage;
        private int _visibleDeviceRows = 1;
        private bool _chargeNowVisible;

        public MainWindow(UiBackend backend)
        {
            _backend = backend;
            Text = "Ayr";
            Icon = _windowIcon;

            _header = new HeaderBand(Theme, "Ayr", MachineIdentity.ModelName);
            _header.MinimizeClicked += () => WindowState = FormWindowState.Minimized;
            _header.CloseClicked += Close;
            _viewport = new ScrollViewport { BackColor = Theme.Background };
            // Content lives on one page that the viewport scrolls, so laying it out never moves the scroll position.
            _page = new Panel { BackColor = Theme.Background };
            _viewport.Controls.Add(_page);

            _error = AddToPage(new ErrorBar(Theme) { Visible = false });

            _statusTitle = AddToPage(Section("Status"));
            _statusCard = AddToPage(new Card(Theme));
            _cpuTemperature = AddTo(_statusCard, new MetricCell(Theme, "CPU temperature", Theme.Value));
            _gpuTemperature = AddTo(_statusCard, new MetricCell(Theme, "GPU temperature", Theme.Value));
            _cpuFan = AddTo(_statusCard, new MetricCell(Theme, "CPU fan", Theme.BodyBold));
            _gpuFan = AddTo(_statusCard, new MetricCell(Theme, "GPU fan", Theme.BodyBold));
            _metrics = new[] { _cpuTemperature, _gpuTemperature, _cpuFan, _gpuFan };

            _fanTitle = AddToPage(Section("Fan"));
            _fan = AddToPage(new Segmented(Theme, FanController.SelectableModes.Select(mode => mode.ToString()).ToArray(), columns: 4));
            _fan.Picked += index => Apply(() => _backend.ApplyFan(FanController.SelectableModes[index]));
            _fanNote = AddToPage(new MutedText(Theme, "Silent switches back to Auto at 90°C.", Theme.Background));

            _performanceTitle = AddToPage(Section("Performance"));
            _performance = AddToPage(new Segmented(Theme, PerformanceController.SelectableLevels.Select(level => level.ToString()).ToArray(), columns: 4));
            _performance.Picked += index => Apply(() => _backend.ApplyPerformance(PerformanceController.SelectableLevels[index]));

            _overclockCard = AddToPage(new Card(Theme));
            _overclockTitle = AddTo(_overclockCard, new TitleWithSubtitle(Theme, "GPU overclock", "Used in Turbo mode", Theme.BodyBold, Theme.Surface));
            _coreOffset = AddTo(_overclockCard, new LabeledSlider(Theme, "Core offset", 0, GpuOffsets.MaxCoreMhz, Megahertz));
            _memoryOffset = AddTo(_overclockCard, new LabeledSlider(Theme, "Memory offset", 0, GpuOffsets.MaxMemoryMhz, Megahertz));
            _applyOverclock = AddTo(_overclockCard, new FlatButton(Theme, "Apply", ButtonStyle.Primary) { BackColor = Theme.Surface });
            _applyOverclock.Click += (sender, e) =>
            {
                var offsets = new GpuOffsets(_coreOffset.Value, _memoryOffset.Value);
                Apply(() => _backend.ApplyTurboOffsets(offsets));
            };

            _appleTitle = AddToPage(new TitleWithSubtitle(Theme, "Apple fast charge", "Fast charging for iPhone and iPad", Theme.Section, Theme.Background));
            _appleToggle = AddToPage(new ToggleSwitch(Theme));
            _appleToggle.Toggled += on => Apply(() => _backend.SetAppleFastCharge(on));
            _appleCard = AddToPage(new Card(Theme));
            for (int row = 0; row < MaxDeviceRows; row++)
            {
                _deviceRows.Add(AddTo(_appleCard, new DeviceRow(Theme)));
            }
            _chargeNow = AddTo(_appleCard, new FlatButton(Theme, "Fast charge now", ButtonStyle.Secondary) { BackColor = Theme.Surface });
            _chargeNow.Click += (sender, e) => Apply(_backend.SendAppleFastChargeNow);

            _settingsTitle = AddToPage(Section("Settings"));
            _settingsCard = AddToPage(new Card(Theme));
            _autostartRow = AddTo(_settingsCard, new TitleWithSubtitle(Theme, "Start with Windows", "Restores your fan and performance settings", Theme.BodyBold, Theme.Surface));
            _autostartToggle = AddTo(_settingsCard, new ToggleSwitch(Theme) { BackColor = Theme.Surface });
            _autostartToggle.Toggled += on => Apply(() => _backend.SetAutostart(on), rereadAutostart: true);

            Controls.Add(_viewport);
            Controls.Add(_header);
            _liveTimer.Tick += (sender, e) => RefreshLive();
            _slowTimer.Tick += (sender, e) => RefreshSlow(includeAutostart: false);
            Relayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshLive();
            RefreshSlow(includeAutostart: true);
            _liveTimer.Start();
            _slowTimer.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            bool visible = WindowState != FormWindowState.Minimized;
            _liveTimer.Enabled = visible;
            _slowTimer.Enabled = visible;
        }

        // With no child focused the wheel reaches the form, which hands it to the content.
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _viewport.ScrollByWheel(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _liveTimer.Stop();
            _slowTimer.Stop();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _liveTimer.Dispose();
                _slowTimer.Dispose();
            }
            base.Dispose(disposing);
            if (disposing)
            {
                _windowIcon.Dispose();
            }
        }

        private static string Megahertz(int value) => "+" + value + " MHz";

        private TitleWithSubtitle Section(string title) => new TitleWithSubtitle(Theme, title, null, Theme.Section, Theme.Background);

        private void Apply(Func<string> change, bool rereadAutostart = false)
        {
            if (_busy)
            {
                return;
            }
            SetBusy(true);
            _backend.Run(this, change, (message, error) =>
            {
                SetBusy(false);
                ShowError(error == null ? null : "Couldn't apply that change: " + error.Message);
                RefreshLive();
                RefreshSlow(rereadAutostart);
            });
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            Control[] inputs = { _fan, _performance, _coreOffset, _memoryOffset, _applyOverclock, _appleToggle, _chargeNow, _autostartToggle };
            foreach (Control input in inputs)
            {
                input.Enabled = !busy;
            }
            UseWaitCursor = busy;
        }

        private void ShowError(string message)
        {
            if (message == _errorMessage)
            {
                return;
            }
            _errorMessage = message;
            _error.Text = message ?? "";
            Relayout();
        }

        private void RefreshLive()
        {
            if (_liveInFlight)
            {
                return;
            }
            _liveInFlight = true;
            _backend.Run(this, _backend.ReadLive, (live, error) =>
            {
                _liveInFlight = false;
                if (error != null)
                {
                    ShowError(ReadErrorPrefix + error.Message);
                    return;
                }
                ShowLive(live);
            });
        }

        private void RefreshSlow(bool includeAutostart)
        {
            if (_slowInFlight)
            {
                return;
            }
            _slowInFlight = true;
            _backend.Run(this, () => _backend.ReadSlow(includeAutostart), (slow, error) =>
            {
                _slowInFlight = false;
                if (error == null)
                {
                    ShowSlow(slow);
                }
            });
        }

        private void ShowLive(LiveState live)
        {
            _cpuTemperature.SetValue(live.Telemetry.CpuTemperature + "°C");
            _gpuTemperature.SetValue(live.Telemetry.GpuTemperature + "°C");
            _cpuFan.SetValue(live.Telemetry.CpuFanRpm.ToString("N0") + " RPM");
            _gpuFan.SetValue(live.Telemetry.GpuFanRpm.ToString("N0") + " RPM");
            _fan.SelectedIndex = FanController.SelectableModes.IndexOf(live.Fan);
            _performance.SelectedIndex = PerformanceController.SelectableLevels.IndexOf(live.Performance);
            bool readErrorShown = _errorMessage != null && _errorMessage.StartsWith(ReadErrorPrefix, StringComparison.Ordinal);
            if (readErrorShown)
            {
                ShowError(null);
            }
        }

        private void ShowSlow(SlowState slow)
        {
            if (!_slidersInitialized)
            {
                // The sliders start at the saved Turbo offsets and are then left to the user.
                _coreOffset.Value = slow.Desired.TurboOffsets.CoreMhz;
                _memoryOffset.Value = slow.Desired.TurboOffsets.MemoryMhz;
                _slidersInitialized = true;
            }
            _appleToggle.Checked = slow.Desired.AppleFastCharge;
            if (slow.AutostartOn.HasValue)
            {
                _autostartToggle.Checked = slow.AutostartOn.Value;
            }
            ShowAppleDevices(slow.AppleDevices);
            ShowMsiCenterWarning(slow.MsiCenterRunning);
        }

        private void ShowMsiCenterWarning(bool msiCenterRunning)
        {
            if (msiCenterRunning)
            {
                ShowError(MsiCenterWarning);
            }
            else if (_errorMessage == MsiCenterWarning)
            {
                ShowError(null);
            }
        }

        private void ShowAppleDevices(IReadOnlyList<AppleDevice> devices)
        {
            if (devices.Count == 0)
            {
                _deviceRows[0].SetDevice("No device connected", "Plug in an iPhone or iPad", connected: false);
            }
            for (int row = 0; row < Math.Min(devices.Count, MaxDeviceRows); row++)
            {
                AppleDevice device = devices[row];
                string detail = device.LastRequest.HasValue ? "Fast charge sent at " + device.LastRequest.Value.ToString("HH:mm") : "Connected";
                _deviceRows[row].SetDevice(device.Model, detail, connected: true);
            }
            int visibleRows = Math.Max(1, Math.Min(MaxDeviceRows, devices.Count));
            bool chargeNowVisible = devices.Count > 0;
            if (visibleRows != _visibleDeviceRows || chargeNowVisible != _chargeNowVisible)
            {
                _visibleDeviceRows = visibleRows;
                _chargeNowVisible = chargeNowVisible;
                Relayout();
            }
        }

        private void Relayout()
        {
            int width = Scaled(BaseWidth);
            int headerHeight = Scaled(HeaderHeight);
            int maxContentHeight = Screen.FromControl(this).WorkingArea.Height - headerHeight - Scaled(ScreenMargin);
            int pageWidth = width;
            int pageHeight = LayoutPage(pageWidth);
            if (pageHeight > maxContentHeight)
            {
                // The scrollbar takes width from the page, so lay out again with nothing under it.
                pageWidth = width - SystemInformation.VerticalScrollBarWidth;
                pageHeight = LayoutPage(pageWidth);
            }
            int contentHeight = Math.Min(pageHeight, maxContentHeight);
            var client = new Size(width + 2, headerHeight + contentHeight + 2);
            if (ClientSize != client)
            {
                ClientSize = client;
            }
            _header.SetBounds(1, 1, width, headerHeight);
            _viewport.SetBounds(1, 1 + headerHeight, width, contentHeight);
            _page.Size = new Size(pageWidth, pageHeight);
        }

        /// <summary>Positions every section for the given page width and returns the page height.</summary>
        private int LayoutPage(int pageWidth)
        {
            int left = Scaled(EdgePadding);
            int inner = pageWidth - 2 * left;
            int cardInner = inner - 2 * Scaled(CardPadding);
            int y = Scaled(12);
            _page.SuspendLayout();
            bool errorShown = _errorMessage != null;
            _error.Visible = errorShown;
            if (errorShown)
            {
                Stack(_error, left, inner, Scaled(38), Scaled(14), ref y);
            }
            LayoutStatus(left, inner, ref y);
            Stack(_fanTitle, left, inner, Scaled(24), Scaled(6), ref y);
            Stack(_fan, left, inner, Scaled(40), Scaled(6), ref y);
            Stack(_fanNote, left, inner, Scaled(22), Scaled(16), ref y);
            Stack(_performanceTitle, left, inner, Scaled(24), Scaled(6), ref y);
            Stack(_performance, left, inner, Scaled(40), Scaled(12), ref y);
            Stack(_overclockCard, left, inner, LayoutOverclockCard(cardInner), Scaled(20), ref y);
            LayoutAppleHeader(left, inner, ref y);
            Stack(_appleCard, left, inner, LayoutAppleCard(cardInner), Scaled(20), ref y);
            Stack(_settingsTitle, left, inner, Scaled(24), Scaled(6), ref y);
            Stack(_settingsCard, left, inner, LayoutSettingsCard(cardInner), Scaled(16), ref y);
            _page.ResumeLayout();
            return y;
        }

        private void LayoutStatus(int left, int inner, ref int y)
        {
            Stack(_statusTitle, left, inner, Scaled(24), Scaled(6), ref y);
            int gap = Scaled(8);
            int cellWidth = (inner - 2 * Scaled(CardPadding) - gap) / 2;
            int cellHeight = Scaled(46);
            int rowGap = Scaled(6);
            for (int index = 0; index < _metrics.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                _metrics[index].SetBounds(Scaled(CardPadding) + column * (cellWidth + gap), Scaled(12) + row * (cellHeight + rowGap), cellWidth, cellHeight);
            }
            Stack(_statusCard, left, inner, 2 * Scaled(12) + 2 * cellHeight + rowGap, Scaled(20), ref y);
        }

        private int LayoutOverclockCard(int cardInner)
        {
            int padding = Scaled(CardPadding);
            int y = padding;
            Stack(_overclockTitle, padding, cardInner, Scaled(38), Scaled(10), ref y);
            Stack(_coreOffset, padding, cardInner, Scaled(44), Scaled(8), ref y);
            Stack(_memoryOffset, padding, cardInner, Scaled(44), Scaled(12), ref y);
            Stack(_applyOverclock, padding, cardInner, Scaled(36), padding, ref y);
            return y;
        }

        private void LayoutAppleHeader(int left, int inner, ref int y)
        {
            _appleTitle.SetBounds(left, y, inner - Scaled(60), Scaled(36));
            _appleToggle.SetBounds(left + inner - Scaled(40), y + Scaled(6), Scaled(40), Scaled(22));
            y += Scaled(42);
        }

        private int LayoutAppleCard(int cardInner)
        {
            int padding = Scaled(CardPadding);
            int y = Scaled(12);
            for (int row = 0; row < MaxDeviceRows; row++)
            {
                bool visible = row < _visibleDeviceRows;
                _deviceRows[row].Visible = visible;
                if (visible)
                {
                    Stack(_deviceRows[row], padding, cardInner, Scaled(40), Scaled(6), ref y);
                }
            }
            _chargeNow.Visible = _chargeNowVisible;
            if (_chargeNowVisible)
            {
                y += Scaled(4);
                Stack(_chargeNow, padding, cardInner, Scaled(36), 0, ref y);
            }
            return y + Scaled(8);
        }

        private int LayoutSettingsCard(int cardInner)
        {
            int padding = Scaled(CardPadding);
            _autostartRow.SetBounds(padding, padding, cardInner - Scaled(60), Scaled(38));
            _autostartToggle.SetBounds(padding + cardInner - Scaled(40), padding + Scaled(8), Scaled(40), Scaled(22));
            return padding + Scaled(38) + padding;
        }

        private static void Stack(Control control, int x, int width, int height, int gapAfter, ref int y)
        {
            control.SetBounds(x, y, width, height);
            y += height + gapAfter;
        }

        private T AddToPage<T>(T control) where T : Control => AddTo(_page, control);

        private static T AddTo<T>(Control parent, T control) where T : Control
        {
            parent.Controls.Add(control);
            return control;
        }
    }
}
