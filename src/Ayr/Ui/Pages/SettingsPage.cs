using System.Collections.Generic;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Ui.Controls;
using Ayr.Updates;

namespace Ayr.Ui.Pages
{
    /// <summary>Starting with Windows, updates, and the installed version.</summary>
    public sealed class SettingsPage : WindowPage
    {
        private const string CheckForUpdatesLabel = "Check for updates";

        private readonly TitleWithSubtitle _title;
        private readonly Card _card;
        private readonly TitleWithSubtitle _autostartRow;
        private readonly ToggleSwitch _autostartToggle;
        private readonly TitleWithSubtitle _autoUpdateRow;
        private readonly ToggleSwitch _autoUpdateToggle;
        private readonly Panel _divider;
        private readonly TitleWithSubtitle _versionRow;
        private readonly FlatButton _updateButton;

        private ReleaseInfo _availableUpdate;
        private bool _updateInFlight;

        public SettingsPage(Theme theme, IPageHost host) : base(theme, host)
        {
            _title = Add(SectionTitle("Settings", null));
            _card = Add(new Card(theme));
            _autostartRow = AddTo(_card, Row("Start with Windows", "Restores your settings when you sign in"));
            _autostartToggle = AddTo(_card, SwitchFor(_autostartRow));
            _autostartToggle.Toggled += on => host.Apply(() => host.Backend.SetAutostart(on));
            _autoUpdateRow = AddTo(_card, Row("Automatic updates", "Installs new versions once a day, when Ayr isn't busy"));
            _autoUpdateToggle = AddTo(_card, SwitchFor(_autoUpdateRow));
            _autoUpdateToggle.Toggled += on => host.Apply(() => host.Backend.SetAutoUpdate(on));
            _divider = AddTo(_card, new Panel { BackColor = theme.Border });
            _versionRow = AddTo(_card, Row("Ayr " + AppVersion.Current, null));
            _updateButton = AddTo(_card, new FlatButton(theme, CheckForUpdatesLabel, ButtonStyle.Secondary));
            _updateButton.Click += (sender, e) => OnUpdateClicked();
        }

        public override string Title => "Settings";

        public override IEnumerable<Control> Inputs => new Control[] { _autostartToggle, _autoUpdateToggle };

        public override void ShowSlow(SlowState slow)
        {
            _autoUpdateToggle.Checked = slow.Desired.AutoUpdate;
            if (slow.AutostartOn.HasValue)
            {
                _autostartToggle.Checked = slow.AutostartOn.Value;
            }
        }

        public override void LayoutContent()
        {
            int padding = Scale(24);
            int width = Width - 2 * padding;
            int y = padding;
            Stack(_title, padding, width, Scale(24), Scale(12), ref y);

            int cardPadding = Scale(CardPadding);
            int inner = width - 2 * cardPadding;
            int cardY = cardPadding;
            LayoutToggleRow(_autostartRow, _autostartToggle, cardPadding, inner, ref cardY);
            cardY += Scale(14);
            LayoutToggleRow(_autoUpdateRow, _autoUpdateToggle, cardPadding, inner, ref cardY);
            cardY += Scale(14);
            Stack(_divider, cardPadding, inner, 1, Scale(14), ref cardY);
            int buttonWidth = Scale(160);
            _versionRow.SetBounds(cardPadding, cardY, inner - buttonWidth - Scale(12), Scale(38));
            _updateButton.SetBounds(cardPadding + inner - buttonWidth, cardY + Scale(2), buttonWidth, Scale(34));
            _card.SetBounds(padding, y, width, cardY + Scale(38) + cardPadding);
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
            Host.Backend.RunUpdate(this, Host.Backend.FindUpdate, (release, error) =>
            {
                if (error != null)
                {
                    ActivityLog.Write("update check failed: " + ExceptionText.Of(error));
                }
                SetUpdateInFlight(false, error != null ? "Couldn't check for updates. Check your internet connection and try again."
                    : release == null ? "You have the latest version"
                    : "Version " + release.Version + " is available");
                _availableUpdate = release;
                _updateButton.Text = release == null ? CheckForUpdatesLabel : "Update to " + release.Version;
            });
        }

        // On success the tray restarts into the new version, which closes this window.
        private void InstallUpdate(ReleaseInfo release)
        {
            string interrupted = Host.Backend.WorkAnExitWouldStop();
            if (interrupted != null)
            {
                _versionRow.SetSubtitle(interrupted + " Update when it's done.");
                return;
            }
            SetUpdateInFlight(true, "Installing " + release.Version + "…");
            Host.Backend.RunUpdate(this, () => Host.Backend.InstallUpdate(release), (message, error) =>
            {
                if (error != null)
                {
                    ActivityLog.Write("update install failed: " + ExceptionText.Of(error));
                }
                SetUpdateInFlight(false, error == null ? message + ", restarting…"
                    : "Couldn't install " + release.Version + ". Ayr is unchanged; try again later.");
            });
        }

        private void SetUpdateInFlight(bool inFlight, string status)
        {
            _updateInFlight = inFlight;
            _updateButton.Inactive = inFlight;
            _versionRow.SetSubtitle(status);
        }
    }
}
