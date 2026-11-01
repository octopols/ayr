using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Ayr.Controllers;
using Ayr.Games;
using Ayr.Ui.Controls;

namespace Ayr.Ui.Pages
{
    /// <summary>
    /// What game mode switches to on the left, the games that start it on the right. The settings default to the highest
    /// of each, and a single button puts them back. The game list scrolls inside its card; the page itself never does.
    /// </summary>
    public sealed class GameModePage : WindowPage
    {
        private const int MaxRows = 32;
        private const int RowHeight = 40;
        private const int RowGap = 6;

        private readonly TitleWithSubtitle _title;
        private readonly Card _settingsCard;
        private readonly TitleWithSubtitle _automaticRow;
        private readonly ToggleSwitch _toggle;
        private readonly Panel _divider;
        private readonly TitleWithSubtitle _performanceLabel;
        private readonly Segmented _performance;
        private readonly TitleWithSubtitle _fanLabel;
        private readonly Segmented _fan;
        private readonly TitleWithSubtitle _overclockRow;
        private readonly ToggleSwitch _overclock;
        private readonly FlatButton _useHighest;
        private readonly Card _gamesCard;
        private readonly ScrollViewport _listViewport;
        private readonly Panel _list;
        private readonly List<ListRow> _rows = new List<ListRow>();
        private readonly MutedText _foundLabel;
        private readonly FlatButton _scan;
        private readonly FlatButton _add;

        private GameProfile _profile = GameProfile.Highest;
        private List<GameEntry> _added = new List<GameEntry>();
        private List<GameEntry> _found = new List<GameEntry>();

        public GameModePage(Theme theme, IPageHost host, ToolTip toolTip) : base(theme, host)
        {
            _title = Add(SectionTitle("Game mode", "While one of your games runs, Ayr switches to these settings, then back to your usual ones."));

            _settingsCard = Add(new Card(theme));
            _automaticRow = AddTo(_settingsCard, Row("Game mode", null));
            _toggle = AddTo(_settingsCard, SwitchFor(_automaticRow));
            _toggle.Toggled += on => host.Apply(() => host.Backend.SetGameMode(on));
            _divider = AddTo(_settingsCard, new Panel { BackColor = theme.Border });
            _performanceLabel = AddTo(_settingsCard, Row("Performance", null));
            _performance = AddTo(_settingsCard, Choices("Performance while gaming", GameProfile.PerformanceChoices));
            _performance.Picked += index => SaveProfile(_profile.WithPerformance(GameProfile.PerformanceChoices[index]));
            _fanLabel = AddTo(_settingsCard, Row("Fans", null));
            _fan = AddTo(_settingsCard, Choices("Fans while gaming", GameProfile.FanChoices));
            _fan.Picked += index => SaveProfile(_profile.WithFan(GameProfile.FanChoices[index]));
            _overclockRow = AddTo(_settingsCard, Row("GPU overclock", null));
            _overclock = AddTo(_settingsCard, SwitchFor(_overclockRow));
            _overclock.Toggled += on => SaveProfile(_profile.WithOverclock(on));
            _useHighest = AddTo(_settingsCard, new FlatButton(theme, "Use the highest settings", ButtonStyle.Secondary));
            _useHighest.Click += (sender, e) => SaveProfile(GameProfile.Highest);

            _gamesCard = Add(new Card(theme));
            _listViewport = AddTo(_gamesCard, new ScrollViewport());
            _list = AddTo(_listViewport, new Panel());
            for (int row = 0; row < MaxRows; row++)
            {
                int index = row;
                ListRow gameRow = AddTo(_list, new ListRow(theme, toolTip, GameRowContent.AddLabel, GameRowContent.RemoveLabel));
                gameRow.ButtonClicked += () => OnRowClicked(index);
                _rows.Add(gameRow);
            }
            _foundLabel = AddTo(_list, new MutedText(theme, "Found on this PC", theme.Surface));
            _scan = AddTo(_gamesCard, new FlatButton(theme, "Scan again", ButtonStyle.Secondary));
            _scan.Click += (sender, e) => host.Apply(host.Backend.ScanForGames);
            _add = AddTo(_gamesCard, new FlatButton(theme, "Add a game…", ButtonStyle.Secondary));
            _add.Click += (sender, e) => BrowseForGame();
        }

        public override string Title => "Game mode";

        public override IEnumerable<Control> Inputs =>
            new Control[] { _toggle, _performance, _fan, _overclock, _useHighest, _scan, _add }.Concat(_rows);

        public override void ShowSlow(SlowState slow)
        {
            ShowSettings(slow);
            ShowGames(slow.Games);
        }

        public override void LayoutContent()
        {
            int padding = Scale(24);
            int y = padding;
            Stack(_title, padding, Width - 2 * padding, Scale(38), Scale(16), ref y);
            int columnWidth = (Width - 2 * padding - Scale(ColumnGap)) / 2;
            LayoutSettings(padding, y, columnWidth);
            LayoutGames(padding + columnWidth + Scale(ColumnGap), y, columnWidth, Height - padding);
        }

        private void LayoutSettings(int x, int y, int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            int cardY = padding;
            LayoutToggleRow(_automaticRow, _toggle, padding, inner, ref cardY);
            cardY += Scale(12);
            Stack(_divider, padding, inner, 1, Scale(14), ref cardY);
            Stack(_performanceLabel, padding, inner, Scale(22), Scale(4), ref cardY);
            Stack(_performance, padding, inner, Scale(32), Scale(14), ref cardY);
            Stack(_fanLabel, padding, inner, Scale(22), Scale(4), ref cardY);
            Stack(_fan, padding, inner, Scale(32), Scale(14), ref cardY);
            LayoutToggleRow(_overclockRow, _overclock, padding, inner, ref cardY);
            _useHighest.Visible = !_profile.Equals(GameProfile.Highest);
            if (_useHighest.Visible)
            {
                cardY += Scale(14);
                Stack(_useHighest, padding, inner, Scale(32), 0, ref cardY);
            }
            _settingsCard.SetBounds(x, y, width, cardY + padding);
        }

        private static string OverclockSubtitle(SlowState slow, GameProfile profile)
        {
            if (!slow.GpuOverclockAvailable)
            {
                return "Ayr can't overclock this graphics card";
            }
            if (profile.Performance != PerformanceLevel.Turbo)
            {
                return "Only works with Turbo";
            }
            return slow.Desired.TurboOffsets.RaisesClocks ? "Uses the amounts on the Performance page" : "Set to +0 on the Performance page";
        }

        private void LayoutGames(int x, int y, int width, int bottom)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            _gamesCard.SetBounds(x, y, width, bottom - y);
            int buttonHeight = Scale(32);
            int listHeight = _gamesCard.Height - 2 * padding - Scale(12) - buttonHeight;
            _listViewport.SetBounds(padding, padding, inner, listHeight);
            LayoutList(inner, listHeight);
            int buttonY = padding + listHeight + Scale(12);
            int gap = Scale(8);
            int buttonWidth = (inner - gap) / 2;
            _scan.SetBounds(padding, buttonY, buttonWidth, buttonHeight);
            _add.SetBounds(padding + buttonWidth + gap, buttonY, inner - buttonWidth - gap, buttonHeight);
        }

        private void ShowSettings(SlowState slow)
        {
            GameEntry playing = slow.Games.Running.FirstOrDefault();
            _toggle.Checked = slow.Desired.GameMode;
            _automaticRow.SetSubtitle(!slow.Desired.GameMode ? "Off. Turn it on to switch whenever one of your games starts"
                : playing != null ? "On now: " + playing.Name
                : "Waiting for one of your games");

            GameProfile profile = slow.Desired.GameProfile;
            _performance.SelectedIndex = GameProfile.PerformanceChoices.IndexOf(profile.Performance);
            _fan.SelectedIndex = GameProfile.FanChoices.IndexOf(profile.Fan);
            _overclock.Checked = profile.Overclock;
            _overclockRow.SetSubtitle(OverclockSubtitle(slow, profile));
            _overclock.Visible = slow.GpuOverclockAvailable;
            bool highestChanged = profile.Equals(GameProfile.Highest) != _profile.Equals(GameProfile.Highest);
            _profile = profile;
            if (highestChanged)
            {
                ContentChanged();
            }
        }

        private void ShowGames(GameOverview games)
        {
            List<GameEntry> added = games.Added.Take(MaxRows).ToList();
            List<GameEntry> found = games.FoundNotAdded.Take(MaxRows - added.Count).ToList();
            for (int row = 0; row < added.Count; row++)
            {
                _rows[row].SetContent(GameRowContent.Added(added[row], games.Running.Any(added[row].SamePathAs)));
            }
            for (int row = 0; row < found.Count; row++)
            {
                _rows[added.Count + row].SetContent(GameRowContent.Found(found[row]));
            }
            if (added.Count + found.Count == 0)
            {
                _rows[0].SetContent(GameRowContent.NoGames);
            }
            bool countsChanged = added.Count != _added.Count || found.Count != _found.Count;
            _added = added;
            _found = found;
            if (countsChanged)
            {
                ContentChanged();
            }
        }

        private void SaveProfile(GameProfile profile) => Host.Apply(() => Host.Backend.SetGameProfile(profile));

        // The rows take the full width until they overflow, then make room for the scrollbar.
        private void LayoutList(int width, int visibleHeight)
        {
            int rowsShown = Math.Max(1, _added.Count + _found.Count);
            int contentHeight = ListHeight(rowsShown);
            int listWidth = contentHeight > visibleHeight ? width - SystemInformation.VerticalScrollBarWidth : width;
            int y = 0;
            _foundLabel.Visible = _found.Count > 0;
            for (int row = 0; row < MaxRows; row++)
            {
                bool visible = row < rowsShown;
                _rows[row].Visible = visible;
                if (!visible)
                {
                    continue;
                }
                if (row == _added.Count && _found.Count > 0)
                {
                    y += _added.Count > 0 ? Scale(RowGap) : 0;
                    Stack(_foundLabel, 0, listWidth, Scale(18), Scale(RowGap), ref y);
                }
                Stack(_rows[row], 0, listWidth, Scale(RowHeight), Scale(RowGap), ref y);
            }
            _list.SetBounds(0, _list.Top, listWidth, y);
        }

        private int ListHeight(int rows)
        {
            int label = _found.Count > 0 ? Scale(18) + Scale(RowGap) + (_added.Count > 0 ? Scale(RowGap) : 0) : 0;
            return rows * (Scale(RowHeight) + Scale(RowGap)) + label;
        }

        private void OnRowClicked(int row)
        {
            if (row < _added.Count)
            {
                GameEntry game = _added[row];
                Host.Apply(() => Host.Backend.RemoveGame(game));
            }
            else if (row - _added.Count < _found.Count)
            {
                GameEntry game = _found[row - _added.Count];
                Host.Apply(() => Host.Backend.AddGame(game));
            }
        }

        private void BrowseForGame()
        {
            using (var dialog = new OpenFileDialog { Title = "Choose the game", Filter = "Games (*.exe)|*.exe" })
            {
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    string path = dialog.FileName;
                    Host.Apply(() => Host.Backend.AddGamePath(path));
                }
            }
        }
    }
}
