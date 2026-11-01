using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Games;
using Ayr.Hardware;
using Ayr.Ui.Controls;
using Ayr.Updates;

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
        private const int MaxGameRows = 16;
        private const int FullChargeIndex = 0;
        private const int CustomChargeIndex = 1;
        private const string MsiCenterWarning = "MSI Center is running and overrides Ayr's settings after sleep. Uninstall MSI Center.";
        private const string CheckForUpdatesLabel = "Check for updates";
        private static readonly TimeSpan LiveInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SlowInterval = TimeSpan.FromSeconds(15);

        private readonly UiBackend _backend;
        private readonly Icon _windowIcon = Brand.LoadIcon(new Size(32, 32));
        private readonly Timer _liveTimer = new Timer { Interval = (int)LiveInterval.TotalMilliseconds };
        private readonly Timer _slowTimer = new Timer { Interval = (int)SlowInterval.TotalMilliseconds };

        private readonly HeaderBand _header;
        private readonly ScrollViewport _viewport;
        private readonly Panel _page;
        private readonly NoticeBar _banner;
        private readonly TitleWithSubtitle _statusTitle;
        private readonly Card _statusCard;
        private readonly MetricCell[] _metrics;
        private readonly MetricCell _cpuTemperature, _gpuTemperature, _cpuFan, _gpuFan;
        private readonly TitleWithSubtitle _fanTitle;
        private readonly Segmented _fan;
        private readonly Card _curveCard;
        private readonly TitleWithSubtitle _curveTitle;
        private readonly Segmented _curveFan;
        private readonly FanCurveEditor _curveEditor;
        private readonly MutedText _curveNote;
        private readonly FlatButton _resetCurve;
        private readonly MutedText _fanNote;
        private readonly TitleWithSubtitle _performanceTitle;
        private readonly Segmented _performance;
        private readonly Card _overclockCard;
        private readonly TitleWithSubtitle _overclockTitle;
        private readonly LabeledSlider _coreOffset, _memoryOffset;
        private readonly FlatButton _applyOverclock;
        private readonly TitleWithSubtitle _gameTitle;
        private readonly Card _gameCard;
        private readonly TitleWithSubtitle _gameAutomaticRow;
        private readonly ToggleSwitch _gameToggle;
        private readonly Panel _gameDivider;
        private readonly List<ListRow> _gameRows = new List<ListRow>();
        private readonly MutedText _gamesFoundLabel;
        private readonly FlatButton _scanGames;
        private readonly FlatButton _addGame;
        private readonly TitleWithSubtitle _laptopTitle;
        private readonly Card _laptopCard;
        private readonly TitleWithSubtitle _chargeRow;
        private readonly Segmented _charge;
        private readonly LabeledSlider _chargeSlider;
        private readonly Panel _laptopDivider;
        private readonly TitleWithSubtitle _lightRow;
        private readonly Segmented _light;
        private readonly TitleWithSubtitle _appleTitle;
        private readonly Card _appleCard;
        private readonly TitleWithSubtitle _appleAutomaticRow;
        private readonly ToggleSwitch _appleToggle;
        private readonly Panel _appleDivider;
        private readonly ThemedToolTip _toolTip;
        private readonly List<ListRow> _deviceRows = new List<ListRow>();
        private readonly TitleWithSubtitle _settingsTitle;
        private readonly Card _settingsCard;
        private readonly TitleWithSubtitle _autostartRow;
        private readonly ToggleSwitch _autostartToggle;
        private readonly TitleWithSubtitle _autoUpdateRow;
        private readonly ToggleSwitch _autoUpdateToggle;
        private readonly TitleWithSubtitle _versionRow;
        private readonly FlatButton _updateButton;

        private bool _slidersInitialized;
        private bool _liveInFlight, _slowInFlight, _busy;
        private string _applyError;
        private string _readError;
        private bool _msiCenterRunning;
        private string _bannerText;
        private int _visibleDeviceRows = 1;
        private List<GameEntry> _shownAddedGames = new List<GameEntry>();
        private List<GameEntry> _shownFoundGames = new List<GameEntry>();
        private ReleaseInfo _availableUpdate;
        private bool _updateInFlight;
        private FanCurves _customCurves = FanCurves.Stock;
        private CurveTemperatures _curveTemperatures;
        private FanKind _editedFan = FanKind.Cpu;
        private bool _curveShown;
        private bool _chargeShown;
        private bool _chargeCustom;
        private int? _shownChargePercent;
        private bool _lightShown;

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

            _banner = AddToPage(new NoticeBar(Theme) { Visible = false });

            _statusTitle = AddToPage(Section("Status"));
            _statusCard = AddToPage(new Card(Theme));
            _cpuTemperature = AddTo(_statusCard, new MetricCell(Theme, "CPU temperature", Theme.Value));
            _gpuTemperature = AddTo(_statusCard, new MetricCell(Theme, "GPU temperature", Theme.Value));
            _cpuFan = AddTo(_statusCard, new MetricCell(Theme, "CPU fan", Theme.BodyBold));
            _gpuFan = AddTo(_statusCard, new MetricCell(Theme, "GPU fan", Theme.BodyBold));
            _metrics = new[] { _cpuTemperature, _gpuTemperature, _cpuFan, _gpuFan };

            _fanTitle = AddToPage(Section("Fan"));
            _fan = AddToPage(new Segmented(Theme, FanController.SelectableModes.Select(mode => mode.ToString()).ToArray(), columns: FanController.SelectableModes.Count));
            _fan.Picked += index => Apply(() => _backend.ApplyFan(FanController.SelectableModes[index]));
            _fanNote = AddToPage(new MutedText(Theme, "Silent switches back to Auto at 90°C.", Theme.Background));

            _curveCard = AddToPage(new Card(Theme));
            _curveTitle = AddTo(_curveCard, new TitleWithSubtitle(Theme, "Custom curve", "Drag a point. It applies when you let go.", Theme.BodyBold, Theme.Surface));
            _curveFan = AddTo(_curveCard, new Segmented(Theme, new[] { "CPU fan", "GPU fan" }, columns: 2) { SelectedIndex = 0 });
            _curveFan.Picked += index =>
            {
                _editedFan = index == 0 ? FanKind.Cpu : FanKind.Gpu;
                _curveFan.SelectedIndex = index;
                ShowCurve();
            };
            _curveEditor = AddTo(_curveCard, new FanCurveEditor(Theme));
            _curveEditor.PointDragged += (point, duty) =>
            {
                _customCurves = _customCurves.With(_editedFan, point, duty);
                ShowCurve();
            };
            _curveEditor.DragFinished += () =>
            {
                FanCurves edited = _customCurves;
                Apply(() => _backend.ApplyCustomCurves(edited));
            };
            _curveNote = AddTo(_curveCard, new MutedText(Theme, "Dashed line: Auto. The hottest points never go below it.", Theme.Surface));
            _resetCurve = AddTo(_curveCard, new FlatButton(Theme, "Reset", ButtonStyle.Secondary) { BackColor = Theme.Surface });
            _resetCurve.Click += (sender, e) => Apply(() => _backend.ApplyCustomCurves(FanCurves.Stock));

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

            _toolTip = new ThemedToolTip(Theme);

            _gameTitle = AddToPage(new TitleWithSubtitle(Theme, "Game mode", "Turbo, GPU overclock and Cooler Boost while you play", Theme.Section, Theme.Background));
            _gameCard = AddToPage(new Card(Theme));
            _gameAutomaticRow = AddTo(_gameCard, new TitleWithSubtitle(Theme, "Turn on while playing", null, Theme.BodyBold, Theme.Surface));
            _gameToggle = AddTo(_gameCard, new ToggleSwitch(Theme) { BackColor = Theme.Surface });
            _gameToggle.Toggled += on => Apply(() => _backend.SetGameMode(on));
            _gameDivider = AddTo(_gameCard, new Panel { BackColor = Theme.Border });
            for (int row = 0; row < MaxGameRows; row++)
            {
                int index = row;
                ListRow gameRow = AddTo(_gameCard, new ListRow(Theme, _toolTip, GameRowContent.AddLabel, GameRowContent.RemoveLabel));
                gameRow.ButtonClicked += () => OnGameRowClicked(index);
                _gameRows.Add(gameRow);
            }
            _gamesFoundLabel = AddTo(_gameCard, new MutedText(Theme, "Found on this PC", Theme.Surface));
            _scanGames = AddTo(_gameCard, new FlatButton(Theme, "Scan again", ButtonStyle.Secondary) { BackColor = Theme.Surface });
            _scanGames.Click += (sender, e) => Apply(_backend.ScanForGames);
            _addGame = AddTo(_gameCard, new FlatButton(Theme, "Add another game…", ButtonStyle.Secondary) { BackColor = Theme.Surface });
            _addGame.Click += (sender, e) => BrowseForGame();

            _laptopTitle = AddToPage(Section("Battery and keyboard"));
            _laptopCard = AddToPage(new Card(Theme));
            _chargeRow = AddTo(_laptopCard, new TitleWithSubtitle(Theme, "Charge limit", "Stopping early helps the battery last if you're mostly plugged in", Theme.BodyBold, Theme.Surface));
            _charge = AddTo(_laptopCard, new Segmented(Theme, new[] { "Full", "Custom" }, columns: 2));
            _charge.Picked += index =>
            {
                int percent = index == FullChargeIndex ? ChargeLimitController.Full : _chargeSlider.Value;
                Apply(() => _backend.ApplyChargeLimit(percent));
            };
            _chargeSlider = AddTo(_laptopCard, new LabeledSlider(Theme, "Stop charging at", ChargeLimitController.MinimumCustom, ChargeLimitController.MaximumCustom, Percent)
            {
                Value = ChargeLimitController.DefaultCustom
            });
            _chargeSlider.ValueCommitted += percent => Apply(() => _backend.ApplyChargeLimit(percent));
            _laptopDivider = AddTo(_laptopCard, new Panel { BackColor = Theme.Border });
            _lightRow = AddTo(_laptopCard, new TitleWithSubtitle(Theme, "Keyboard light", "The Fn keys change it too. Fn+F7 opens Ayr.", Theme.BodyBold, Theme.Surface));
            _light = AddTo(_laptopCard, new Segmented(Theme, KeyboardLightController.LevelNames.ToArray(), columns: KeyboardLightController.LevelNames.Count));
            _light.Picked += level => Apply(() => _backend.ApplyKeyboardLight(level));

            _appleTitle = AddToPage(new TitleWithSubtitle(Theme, "Apple fast charge", "Faster charging for iPhone and iPad", Theme.Section, Theme.Background));
            _appleCard = AddToPage(new Card(Theme));
            _appleAutomaticRow = AddTo(_appleCard, new TitleWithSubtitle(Theme, "Send automatically", "Every iPhone or iPad gets the request when plugged in", Theme.BodyBold, Theme.Surface));
            _appleToggle = AddTo(_appleCard, new ToggleSwitch(Theme) { BackColor = Theme.Surface });
            _appleToggle.Toggled += on => Apply(() => _backend.SetAppleAutoFastCharge(on));
            _appleDivider = AddTo(_appleCard, new Panel { BackColor = Theme.Border });
            for (int row = 0; row < MaxDeviceRows; row++)
            {
                ListRow deviceRow = AddTo(_appleCard, new ListRow(Theme, _toolTip, AppleRowContent.SendLabel, AppleRowContent.SentLabel));
                deviceRow.ButtonClicked += () => Apply(_backend.SendAppleFastCharge);
                _deviceRows.Add(deviceRow);
            }

            _settingsTitle = AddToPage(Section("Settings"));
            _settingsCard = AddToPage(new Card(Theme));
            _autostartRow = AddTo(_settingsCard, new TitleWithSubtitle(Theme, "Start with Windows", "Restores your fan and performance settings", Theme.BodyBold, Theme.Surface));
            _autostartToggle = AddTo(_settingsCard, new ToggleSwitch(Theme) { BackColor = Theme.Surface });
            _autostartToggle.Toggled += on => Apply(() => _backend.SetAutostart(on), rereadAutostart: true);
            _autoUpdateRow = AddTo(_settingsCard, new TitleWithSubtitle(Theme, "Automatic updates", "Installs new versions from GitHub", Theme.BodyBold, Theme.Surface));
            _autoUpdateToggle = AddTo(_settingsCard, new ToggleSwitch(Theme) { BackColor = Theme.Surface });
            _autoUpdateToggle.Toggled += on => Apply(() => _backend.SetAutoUpdate(on));
            _versionRow = AddTo(_settingsCard, new TitleWithSubtitle(Theme, "Ayr " + AppVersion.Current, null, Theme.BodyBold, Theme.Surface));
            _updateButton = AddTo(_settingsCard, new FlatButton(Theme, CheckForUpdatesLabel, ButtonStyle.Secondary) { BackColor = Theme.Surface });
            _updateButton.Click += (sender, e) => OnUpdateClicked();

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
                _toolTip.Dispose();
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
                _applyError = error == null ? null : "Couldn't apply that change: " + error.Message;
                UpdateBanner();
                RefreshLive();
                RefreshSlow(rereadAutostart);
            });
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            Control[] inputs =
            {
                _fan, _curveEditor, _resetCurve, _performance, _coreOffset, _memoryOffset, _applyOverclock, _gameToggle, _scanGames, _addGame,
                _charge, _chargeSlider, _light, _appleToggle, _autostartToggle, _autoUpdateToggle
            };
            foreach (Control input in inputs.Concat(_gameRows).Concat(_deviceRows))
            {
                input.Enabled = !busy;
            }
            UseWaitCursor = busy;
        }

        private void OnUpdateClicked()
        {
            if (_updateInFlight)
            {
                return;
            }
            if (_availableUpdate == null)
            {
                CheckForUpdate();
            }
            else
            {
                InstallUpdate(_availableUpdate);
            }
        }

        private void CheckForUpdate()
        {
            SetUpdateInFlight(true, "Checking for updates…");
            _backend.RunUpdate(this, _backend.FindUpdate, (release, error) =>
            {
                SetUpdateInFlight(false, error != null ? "Couldn't check: " + error.Message
                    : release == null ? "You have the latest version"
                    : "Version " + release.Version + " is available");
                _availableUpdate = release;
                _updateButton.Text = release == null ? CheckForUpdatesLabel : "Update to " + release.Version;
            });
        }

        // On success the tray restarts into the new version, which closes this window.
        private void InstallUpdate(ReleaseInfo release)
        {
            SetUpdateInFlight(true, "Installing " + release.Version + "…");
            _backend.RunUpdate(this, () => _backend.InstallUpdate(release), (message, error) =>
                SetUpdateInFlight(false, error == null ? message + ", restarting…" : "Update failed: " + error.Message));
        }

        private void SetUpdateInFlight(bool inFlight, string status)
        {
            _updateInFlight = inFlight;
            _updateButton.Inactive = inFlight;
            _versionRow.SetSubtitle(status);
        }

        // One line at the top of the window: a failed change matters most, then unreadable hardware, then MSI Center.
        private void UpdateBanner()
        {
            string text = _applyError ?? _readError ?? (_msiCenterRunning ? MsiCenterWarning : null);
            if (text == _bannerText)
            {
                return;
            }
            _bannerText = text;
            _banner.Text = text ?? "";
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
                _readError = error == null ? null : "Couldn't read the hardware: " + error.Message;
                UpdateBanner();
                if (error == null)
                {
                    ShowLive(live);
                }
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
            if (live.KeyboardLight.HasValue)
            {
                _light.SelectedIndex = live.KeyboardLight.Value;
            }
            bool curveShown = live.Fan == FanMode.Custom;
            bool lightShown = live.KeyboardLight.HasValue;
            if (curveShown != _curveShown || lightShown != _lightShown)
            {
                _curveShown = curveShown;
                _lightShown = lightShown;
                Relayout();
            }
        }

        private void ShowCurve()
        {
            if (_curveTemperatures == null)
            {
                return;
            }
            IReadOnlyList<byte> stock = FanCurves.Stock.For(_editedFan);
            IReadOnlyList<byte> minimums = Enumerable.Range(0, stock.Count).Select(point => FanCurves.MinimumDuty(_editedFan, point)).ToList();
            _curveEditor.SetCurve(_customCurves.For(_editedFan), stock, minimums, _curveTemperatures.For(_editedFan));
        }

        private static string Percent(int value) => value + "%";

        // The slider shows the battery's own limit when one is set, otherwise the last custom value, ready for Custom.
        private void ShowChargeLimit(SlowState slow)
        {
            bool shown = slow.ChargeLimitPercent.HasValue;
            bool custom = shown && slow.ChargeLimitPercent.Value != ChargeLimitController.Full;
            if (shown && slow.ChargeLimitPercent != _shownChargePercent)
            {
                _shownChargePercent = slow.ChargeLimitPercent;
                _charge.SelectedIndex = custom ? CustomChargeIndex : FullChargeIndex;
                _chargeSlider.Value = custom ? slow.ChargeLimitPercent.Value : slow.Desired.CustomChargePercent;
            }
            if (shown != _chargeShown || custom != _chargeCustom)
            {
                _chargeShown = shown;
                _chargeCustom = custom;
                Relayout();
            }
        }

        private void ShowSlow(SlowState slow)
        {
            _curveTemperatures = slow.CurveTemperatures;
            if (!_curveEditor.IsDragging && !_busy)
            {
                _customCurves = slow.Desired.CustomCurves;
            }
            ShowCurve();
            ShowChargeLimit(slow);
            if (!_slidersInitialized)
            {
                // The sliders start at the saved Turbo offsets and are then left to the user.
                _coreOffset.Value = slow.Desired.TurboOffsets.CoreMhz;
                _memoryOffset.Value = slow.Desired.TurboOffsets.MemoryMhz;
                _slidersInitialized = true;
            }
            _appleToggle.Checked = slow.Desired.AppleAutoFastCharge;
            _autoUpdateToggle.Checked = slow.Desired.AutoUpdate;
            if (slow.AutostartOn.HasValue)
            {
                _autostartToggle.Checked = slow.AutostartOn.Value;
            }
            ShowGames(slow);
            ShowAppleDevices(slow);
            _msiCenterRunning = slow.MsiCenterRunning;
            UpdateBanner();
        }

        private void ShowGames(SlowState slow)
        {
            GameOverview games = slow.Games;
            _gameToggle.Checked = slow.Desired.GameMode;
            GameEntry playing = games.Running.FirstOrDefault();
            _gameAutomaticRow.SetSubtitle(!slow.Desired.GameMode ? "Starts when one of your games starts"
                : playing != null ? "On now: " + playing.Name + " is running"
                : "Waiting for one of your games");

            List<GameEntry> added = games.Added.Take(MaxGameRows).ToList();
            List<GameEntry> found = games.FoundNotAdded.Take(MaxGameRows - added.Count).ToList();
            for (int row = 0; row < added.Count; row++)
            {
                _gameRows[row].SetContent(GameRowContent.Added(added[row], games.Running.Any(added[row].SamePathAs)));
            }
            for (int row = 0; row < found.Count; row++)
            {
                _gameRows[added.Count + row].SetContent(GameRowContent.Found(found[row]));
            }
            if (added.Count + found.Count == 0)
            {
                _gameRows[0].SetContent(GameRowContent.NoGames);
            }
            bool layoutChanged = added.Count != _shownAddedGames.Count || found.Count != _shownFoundGames.Count;
            _shownAddedGames = added;
            _shownFoundGames = found;
            if (layoutChanged)
            {
                Relayout();
            }
        }

        private void OnGameRowClicked(int row)
        {
            if (row < _shownAddedGames.Count)
            {
                GameEntry game = _shownAddedGames[row];
                Apply(() => _backend.RemoveGame(game));
            }
            else if (row - _shownAddedGames.Count < _shownFoundGames.Count)
            {
                GameEntry game = _shownFoundGames[row - _shownAddedGames.Count];
                Apply(() => _backend.AddGame(game));
            }
        }

        private void BrowseForGame()
        {
            using (var dialog = new OpenFileDialog { Title = "Choose the game", Filter = "Games (*.exe)|*.exe" })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string path = dialog.FileName;
                    Apply(() => _backend.AddGamePath(path));
                }
            }
        }

        private void ShowAppleDevices(SlowState slow)
        {
            int connected = slow.AppleDevices.Count;
            if (connected == 0)
            {
                _deviceRows[0].SetContent(AppleRowContent.NoDevice);
            }
            for (int row = 0; row < Math.Min(connected, MaxDeviceRows); row++)
            {
                _deviceRows[row].SetContent(AppleRowContent.For(slow.AppleDevices[row], slow.Desired.AppleAutoFastCharge, slow.NextAppleRequestAllowedAt));
            }
            int visibleRows = Math.Max(1, Math.Min(MaxDeviceRows, connected));
            if (visibleRows != _visibleDeviceRows)
            {
                _visibleDeviceRows = visibleRows;
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
            bool bannerShown = _bannerText != null;
            _banner.Visible = bannerShown;
            if (bannerShown)
            {
                Stack(_banner, left, inner, Scaled(38), Scaled(14), ref y);
            }
            LayoutStatus(left, inner, ref y);
            Stack(_fanTitle, left, inner, Scaled(24), Scaled(6), ref y);
            Stack(_fan, left, inner, Scaled(40), Scaled(6), ref y);
            Stack(_fanNote, left, inner, Scaled(22), _curveShown ? Scaled(6) : Scaled(16), ref y);
            _curveCard.Visible = _curveShown;
            if (_curveShown)
            {
                Stack(_curveCard, left, inner, LayoutCurveCard(cardInner), Scaled(20), ref y);
            }
            Stack(_performanceTitle, left, inner, Scaled(24), Scaled(6), ref y);
            Stack(_performance, left, inner, Scaled(40), Scaled(12), ref y);
            Stack(_overclockCard, left, inner, LayoutOverclockCard(cardInner), Scaled(20), ref y);
            Stack(_gameTitle, left, inner, Scaled(36), Scaled(8), ref y);
            Stack(_gameCard, left, inner, LayoutGameCard(cardInner), Scaled(20), ref y);
            bool laptopShown = _chargeShown || _lightShown;
            _laptopTitle.Visible = laptopShown;
            _laptopCard.Visible = laptopShown;
            if (laptopShown)
            {
                Stack(_laptopTitle, left, inner, Scaled(24), Scaled(6), ref y);
                Stack(_laptopCard, left, inner, LayoutLaptopCard(cardInner), Scaled(20), ref y);
            }
            Stack(_appleTitle, left, inner, Scaled(36), Scaled(8), ref y);
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

        private int LayoutCurveCard(int cardInner)
        {
            int padding = Scaled(CardPadding);
            int y = padding;
            Stack(_curveTitle, padding, cardInner, Scaled(38), Scaled(8), ref y);
            Stack(_curveFan, padding, cardInner, Scaled(32), Scaled(8), ref y);
            Stack(_curveEditor, padding, cardInner, Scaled(180), Scaled(8), ref y);
            int buttonWidth = Scaled(90);
            _curveNote.SetBounds(padding, y, cardInner - buttonWidth - Scaled(8), Scaled(32));
            _resetCurve.SetBounds(padding + cardInner - buttonWidth, y, buttonWidth, Scaled(32));
            return y + Scaled(32) + padding;
        }

        private int LayoutLaptopCard(int cardInner)
        {
            int padding = Scaled(CardPadding);
            int y = padding;
            _chargeRow.Visible = _chargeShown;
            _charge.Visible = _chargeShown;
            _chargeSlider.Visible = _chargeShown && _chargeCustom;
            _laptopDivider.Visible = _chargeShown && _lightShown;
            _lightRow.Visible = _lightShown;
            _light.Visible = _lightShown;
            if (_chargeShown)
            {
                Stack(_chargeRow, padding, cardInner, Scaled(38), Scaled(8), ref y);
                Stack(_charge, padding, cardInner, Scaled(36), Scaled(12), ref y);
                if (_chargeCustom)
                {
                    Stack(_chargeSlider, padding, cardInner, Scaled(44), Scaled(12), ref y);
                }
            }
            if (_chargeShown && _lightShown)
            {
                Stack(_laptopDivider, padding, cardInner, 1, Scaled(12), ref y);
            }
            if (_lightShown)
            {
                Stack(_lightRow, padding, cardInner, Scaled(38), Scaled(8), ref y);
                Stack(_light, padding, cardInner, Scaled(36), Scaled(12), ref y);
            }
            return y - Scaled(12) + padding;
        }

        private int LayoutGameCard(int cardInner)
        {
            int padding = Scaled(CardPadding);
            int y = padding;
            LayoutToggleRow(_gameAutomaticRow, _gameToggle, padding, cardInner, ref y);
            y += Scaled(12);
            Stack(_gameDivider, padding, cardInner, 1, Scaled(12), ref y);
            int addedRows = _shownAddedGames.Count;
            int foundRows = _shownFoundGames.Count;
            int visibleRows = Math.Max(1, addedRows + foundRows);
            _gamesFoundLabel.Visible = foundRows > 0;
            for (int row = 0; row < MaxGameRows; row++)
            {
                bool visible = row < visibleRows;
                _gameRows[row].Visible = visible;
                if (!visible)
                {
                    continue;
                }
                if (row == addedRows && foundRows > 0)
                {
                    y += addedRows > 0 ? Scaled(6) : 0;
                    Stack(_gamesFoundLabel, padding, cardInner, Scaled(18), Scaled(6), ref y);
                }
                Stack(_gameRows[row], padding, cardInner, Scaled(40), Scaled(6), ref y);
            }
            y += Scaled(4);
            int gap = Scaled(8);
            int buttonWidth = (cardInner - gap) / 2;
            _scanGames.SetBounds(padding, y, buttonWidth, Scaled(36));
            _addGame.SetBounds(padding + buttonWidth + gap, y, cardInner - buttonWidth - gap, Scaled(36));
            return y + Scaled(36) + padding;
        }

        private int LayoutAppleCard(int cardInner)
        {
            int padding = Scaled(CardPadding);
            int y = padding;
            LayoutToggleRow(_appleAutomaticRow, _appleToggle, padding, cardInner, ref y);
            y += Scaled(12);
            Stack(_appleDivider, padding, cardInner, 1, Scaled(12), ref y);
            for (int row = 0; row < MaxDeviceRows; row++)
            {
                bool visible = row < _visibleDeviceRows;
                _deviceRows[row].Visible = visible;
                if (visible)
                {
                    Stack(_deviceRows[row], padding, cardInner, Scaled(40), Scaled(6), ref y);
                }
            }
            return y + Scaled(8);
        }

        private int LayoutSettingsCard(int cardInner)
        {
            int padding = Scaled(CardPadding);
            int rowGap = Scaled(12);
            int y = padding;
            LayoutToggleRow(_autostartRow, _autostartToggle, padding, cardInner, ref y);
            y += rowGap;
            LayoutToggleRow(_autoUpdateRow, _autoUpdateToggle, padding, cardInner, ref y);
            y += rowGap;
            int buttonWidth = Scaled(150);
            _versionRow.SetBounds(padding, y, cardInner - buttonWidth - Scaled(12), Scaled(38));
            _updateButton.SetBounds(padding + cardInner - buttonWidth, y + Scaled(3), buttonWidth, Scaled(32));
            return y + Scaled(38) + padding;
        }

        private void LayoutToggleRow(TitleWithSubtitle row, ToggleSwitch toggle, int x, int width, ref int y)
        {
            row.SetBounds(x, y, width - Scaled(60), Scaled(38));
            toggle.SetBounds(x + width - Scaled(40), y + Scaled(8), Scaled(40), Scaled(22));
            y += Scaled(38);
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
