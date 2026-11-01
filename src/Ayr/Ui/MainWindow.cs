using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Hardware;
using Ayr.Ui.Controls;
using Ayr.Ui.Pages;

namespace Ayr.Ui
{
    /// <summary>
    /// The full window: a fixed size with every setting on one of its pages, so nothing ever scrolls. The sidebar lists
    /// the pages and keeps temperatures and fan speeds in view on every page. The window exists only while open, and its
    /// timers run only while it is visible, so the tray process stays near idle the rest of the time.
    /// </summary>
    public sealed class MainWindow : PopupWindow, IPageHost
    {
        private const int BaseWidth = 860;
        private const int BaseHeight = 580;
        private const int HeaderHeight = 64;
        private const int SidebarWidth = 200;
        private const int SidebarPadding = 12;
        private const int NavigationRowHeight = 40;
        private const int BannerHeight = 38;
        private const string MsiSoftwareNotice = "MSI software is still installed and can undo your settings.";
        private const string MsiSoftwareAction = "Review";
        private static readonly TimeSpan LiveInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SlowInterval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ConfirmationShownFor = TimeSpan.FromSeconds(5);

        private readonly Icon _windowIcon = Brand.LoadIcon(new Size(32, 32));
        private readonly Timer _liveTimer = new Timer { Interval = (int)LiveInterval.TotalMilliseconds };
        private readonly Timer _slowTimer = new Timer { Interval = (int)SlowInterval.TotalMilliseconds };
        private readonly ThemedToolTip _toolTip;

        private readonly HeaderBand _header;
        private readonly Panel _sidebar;
        private readonly Panel _sidebarEdge;
        private readonly NavigationList _navigation;
        private readonly SidebarStatus _status;
        private readonly MutedText _confirmation;
        private readonly Timer _confirmationTimer = new Timer { Interval = (int)ConfirmationShownFor.TotalMilliseconds };
        private readonly NoticeBar _banner;
        private readonly WindowPage[] _pages;
        private readonly MsiSoftwarePage _msiPage;
        private WindowPage _currentPage;

        private bool _liveInFlight, _slowInFlight, _busy;
        private string _applyError;
        private string _readError;
        private bool _msiCenterRunning;
        private int _msiItemsFound;
        private string _bannerText;
        private string _bannerAction;

        public MainWindow(UiBackend backend)
        {
            Backend = backend;
            Text = "Ayr";
            Icon = _windowIcon;
            _toolTip = new ThemedToolTip(Theme);

            _header = new HeaderBand(Theme, "Ayr", MachineIdentity.ModelName);
            _header.MinimizeClicked += () => WindowState = FormWindowState.Minimized;
            _header.CloseClicked += Close;

            _msiPage = new MsiSoftwarePage(Theme, this);
            _msiPage.SoftwareCounted += count =>
            {
                _msiItemsFound = count;
                UpdateBanner();
            };
            _pages = new WindowPage[]
            {
                new PerformancePage(Theme, this),
                new GameModePage(Theme, this, _toolTip),
                new BatteryPage(Theme, this),
                new AppleChargingPage(Theme, this, _toolTip),
                new KeyboardPage(Theme, this),
                _msiPage,
                new SettingsPage(Theme, this)
            };
            foreach (WindowPage page in _pages)
            {
                page.Visible = false;
                Controls.Add(page);
            }

            _sidebar = new Panel { BackColor = Theme.Background };
            _sidebarEdge = new Panel { BackColor = Theme.Border };
            _navigation = new NavigationList(Theme, _pages.Select(page => page.Title).ToList(), Scaled(NavigationRowHeight));
            _navigation.Picked += index => ShowPage(_pages[index]);
            _sidebar.Controls.Add(_navigation);
            _status = new SidebarStatus(Theme);
            _sidebar.Controls.Add(_status);
            _confirmation = new MutedText(Theme, "", Theme.Background) { Wraps = true };
            _sidebar.Controls.Add(_confirmation);
            _confirmationTimer.Tick += (sender, e) =>
            {
                _confirmationTimer.Stop();
                _confirmation.Text = "";
            };

            _banner = new NoticeBar(Theme) { Visible = false };
            _banner.ActionClicked += () => ShowPage(_msiPage);

            Controls.Add(_banner);
            Controls.Add(_sidebarEdge);
            Controls.Add(_sidebar);
            Controls.Add(_header);
            _liveTimer.Tick += (sender, e) => RefreshLive();
            _slowTimer.Tick += (sender, e) => RefreshSlow(includeAutostart: false);
            ShowPage(_pages[0]);
        }

        public UiBackend Backend { get; }

        public int PageCount => _pages.Length;

        public int Scale(int pixels) => Scaled(pixels);

        public void ShowPage(int index) => ShowPage(_pages[index]);

        public void Apply(Func<string> change)
        {
            if (_busy)
            {
                return;
            }
            SetBusy(true);
            Backend.Run(this, change, (message, error) =>
            {
                SetBusy(false);
                _applyError = error == null ? null : "Couldn't apply that change: " + ExceptionText.Of(error);
                if (error == null)
                {
                    ShowConfirmation(message);
                }
                UpdateBanner();
                RefreshLive();
                RefreshSlow(includeAutostart: true);
            });
        }

        public void RefreshState()
        {
            RefreshLive();
            RefreshSlow(includeAutostart: false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshLive();
            RefreshSlow(includeAutostart: true);
            _liveTimer.Start();
            _slowTimer.Start();
            _msiPage.ScanOnce();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            bool visible = WindowState != FormWindowState.Minimized;
            _liveTimer.Enabled = visible;
            _slowTimer.Enabled = visible;
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
                _confirmationTimer.Dispose();
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
            if (disposing)
            {
                _windowIcon.Dispose();
            }
        }

        private void ShowPage(WindowPage page)
        {
            _currentPage = page;
            _navigation.SelectedIndex = Array.IndexOf(_pages, page);
            foreach (WindowPage other in _pages)
            {
                other.Visible = other == page;
            }
            UpdateBanner();
            Relayout();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (Control input in _pages.SelectMany(page => page.Inputs))
            {
                input.Enabled = !busy;
            }
            UseWaitCursor = busy;
        }

        // Every change says what it did, for a few seconds, in a spot where it never moves the page around.
        private void ShowConfirmation(string message)
        {
            _confirmation.Text = message ?? "";
            _confirmationTimer.Stop();
            _confirmationTimer.Start();
        }

        // One line above the page: a failed change matters most, then unreadable hardware, then MSI software still installed.
        private void UpdateBanner()
        {
            string error = _applyError ?? _readError;
            bool msiInstalled = (_msiItemsFound > 0 || _msiCenterRunning) && _currentPage != _msiPage;
            string text = error ?? (msiInstalled ? MsiSoftwareNotice : null);
            string action = error == null && msiInstalled ? MsiSoftwareAction : null;
            if (text == _bannerText && action == _bannerAction)
            {
                return;
            }
            _bannerText = text;
            _bannerAction = action;
            _banner.SetMessage(text ?? "", action);
            Relayout();
        }

        private void RefreshLive()
        {
            if (_liveInFlight)
            {
                return;
            }
            _liveInFlight = true;
            Backend.Run(this, Backend.ReadLive, (live, error) =>
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
            Backend.Run(this, () => Backend.ReadSlow(includeAutostart), (slow, error) =>
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
            _status.ShowReading(live.Telemetry);
            foreach (WindowPage page in _pages)
            {
                page.ShowLive(live);
            }
        }

        private void ShowSlow(SlowState slow)
        {
            foreach (WindowPage page in _pages)
            {
                page.ShowSlow(slow);
            }
            _msiCenterRunning = slow.MsiCenterRunning;
            UpdateBanner();
        }

        private void Relayout()
        {
            int width = Scaled(BaseWidth);
            int height = Scaled(BaseHeight);
            var client = new Size(width + 2, height + 2);
            if (ClientSize != client)
            {
                ClientSize = client;
            }
            int headerHeight = Scaled(HeaderHeight);
            int bodyTop = 1 + headerHeight;
            int bodyHeight = height - headerHeight;
            int sidebarWidth = Scaled(SidebarWidth);
            _header.SetBounds(1, 1, width, headerHeight);
            _sidebar.SetBounds(1, bodyTop, sidebarWidth, bodyHeight);
            _sidebarEdge.SetBounds(1 + sidebarWidth, bodyTop, 1, bodyHeight);
            LayoutSidebar(sidebarWidth, bodyHeight);

            int pageLeft = 2 + sidebarWidth;
            int pageWidth = width - sidebarWidth - 1;
            int pageTop = bodyTop;
            bool bannerShown = _bannerText != null;
            _banner.Visible = bannerShown;
            if (bannerShown)
            {
                int margin = Scaled(16);
                _banner.SetBounds(pageLeft + margin, pageTop + margin, pageWidth - 2 * margin, Scaled(BannerHeight));
                pageTop += margin + Scaled(BannerHeight);
            }
            _currentPage.SetBounds(pageLeft, pageTop, pageWidth, bodyTop + bodyHeight - pageTop);
            _currentPage.LayoutContent();
        }

        private void LayoutSidebar(int width, int height)
        {
            int padding = Scaled(SidebarPadding);
            int inner = width - 2 * padding;
            _navigation.SetBounds(padding, Scaled(16), inner, _navigation.PreferredHeight);
            int statusHeight = _status.PreferredHeight(Scaled(26));
            _status.SetBounds(padding, height - Scaled(16) - statusHeight, inner, statusHeight);
            int confirmationHeight = Scaled(36);
            _confirmation.SetBounds(padding, _status.Top - Scaled(8) - confirmationHeight, inner, confirmationHeight);
        }
    }
}
