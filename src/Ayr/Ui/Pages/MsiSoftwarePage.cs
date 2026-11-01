using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Ayr.App;
using Ayr.MsiRemoval;
using Ayr.Ui.Controls;

namespace Ayr.Ui.Pages
{
    /// <summary>
    /// Everything of MSI's on this PC, grouped, each with a switch, and one button to remove what is switched on. Removing
    /// takes two presses, the second after the page has said what will happen. The window scans when it opens, so it can
    /// point here from its banner.
    /// </summary>
    public sealed class MsiSoftwarePage : WindowPage
    {
        private const int RowHeight = 44;
        private const int RowGap = 6;
        private const int HeadingHeight = 22;
        private const string RemoveLabel = "Remove selected";
        private const string ConfirmLabel = "Confirm removal";

        private enum Stage
        {
            NotScanned,
            Scanning,
            Choosing,
            Confirming,
            Removing,
            Finished
        }

        private enum Group
        {
            Apps,
            ServicesAndDrivers,
            Leftovers
        }

        private sealed class Entry
        {
            public Entry(MsiComponent component, SelectableRow row)
            {
                Component = component;
                Row = row;
            }

            public MsiComponent Component { get; }
            public SelectableRow Row { get; }
            public Group Group => GroupOf(Component.Kind);
        }

        private readonly TitleWithSubtitle _title;
        private readonly Card _card;
        private readonly TitleWithSubtitle _selectAllRow;
        private readonly ToggleSwitch _selectAll;
        private readonly Panel _divider;
        private readonly ScrollViewport _viewport;
        private readonly Panel _list;
        private readonly Dictionary<Group, MutedText> _headings = new Dictionary<Group, MutedText>();
        private readonly Panel _footerDivider;
        private readonly TitleWithSubtitle _restorePointRow;
        private readonly ToggleSwitch _restorePoint;
        private readonly MutedText _status;
        private readonly FlatButton _scan;
        private readonly FlatButton _remove;
        private readonly FlatButton _openBackup;
        private readonly TitleWithSubtitle _nothingFound;
        private readonly List<Entry> _entries = new List<Entry>();

        private Stage _stage = Stage.NotScanned;
        private string _backupFolder;
        private bool _scanFailed;

        public MsiSoftwarePage(Theme theme, IPageHost host) : base(theme, host)
        {
            _title = Add(SectionTitle("MSI software", "MSI's apps and services undo your settings in the background. Choose what to remove."));
            _card = Add(new Card(theme));
            _selectAllRow = AddTo(_card, Row("Select all", null));
            _selectAll = AddTo(_card, SwitchFor(_selectAllRow));
            _selectAll.Toggled += SelectAll;
            _divider = AddTo(_card, new Panel { BackColor = theme.Border });
            _viewport = AddTo(_card, new ScrollViewport());
            _list = AddTo(_viewport, new Panel());
            foreach (Group group in Enum.GetValues(typeof(Group)))
            {
                _headings[group] = AddTo(_list, new MutedText(theme, HeadingOf(group), theme.Surface));
            }
            _footerDivider = AddTo(_card, new Panel { BackColor = theme.Border });
            _restorePointRow = AddTo(_card, Row("Make a restore point first", "Takes longer, and only works if System Restore is turned on"));
            _restorePoint = AddTo(_card, SwitchFor(_restorePointRow));
            _restorePoint.Toggled += on => _restorePoint.Checked = on;
            _status = AddTo(_card, new MutedText(theme, "", theme.Surface));
            _scan = AddTo(_card, new FlatButton(theme, "Scan again", ButtonStyle.Secondary));
            _scan.Click += (sender, e) => Scan();
            _remove = AddTo(_card, new FlatButton(theme, RemoveLabel, ButtonStyle.Primary));
            _remove.Click += (sender, e) => OnRemoveClicked();
            _nothingFound = AddTo(_card, Row("No MSI software on this PC", "Nothing in the background is undoing your settings."));
            _openBackup = AddTo(_card, new FlatButton(theme, "Open backup", ButtonStyle.Secondary) { Visible = false });
            _openBackup.Click += (sender, e) => Process.Start("explorer.exe", "\"" + _backupFolder + "\"")?.Dispose();
            ShowStage(Stage.NotScanned);
        }

        /// <summary>Raised after each scan or removal with how many items of MSI's are still on this PC.</summary>
        public event Action<int> SoftwareCounted;

        public override string Title => "MSI software";

        public override IEnumerable<Control> Inputs => new Control[] { _scan, _remove, _selectAll, _restorePoint };

        public void ScanOnce()
        {
            if (_stage == Stage.NotScanned)
            {
                Scan();
            }
        }

        public override void LayoutContent()
        {
            int padding = Scale(24);
            int width = Width - 2 * padding;
            int y = padding;
            Stack(_title, padding, width, Scale(38), Scale(16), ref y);
            bool hasItems = _entries.Count > 0;
            foreach (Control choosing in new Control[] { _selectAllRow, _selectAll, _divider, _viewport, _footerDivider, _restorePointRow, _restorePoint, _remove })
            {
                choosing.Visible = hasItems;
            }
            _nothingFound.Visible = !hasItems && _stage != Stage.Scanning && !_scanFailed;
            if (hasItems)
            {
                _card.SetBounds(padding, y, width, Height - padding - y);
                LayoutChoices(width);
            }
            else
            {
                _card.SetBounds(padding, y, width, LayoutNothingFound(width));
            }
        }

        // With nothing to remove, the card says so and offers only a new scan.
        private int LayoutNothingFound(int width)
        {
            int cardPadding = Scale(CardPadding);
            int inner = width - 2 * cardPadding;
            int scanWidth = Scale(110);
            int rowHeight = Scale(38);
            _nothingFound.SetBounds(cardPadding, cardPadding, inner - scanWidth - Scale(12), rowHeight);
            _scan.SetBounds(cardPadding + inner - scanWidth, cardPadding + Scale(2), scanWidth, Scale(34));
            _status.SetBounds(cardPadding, cardPadding, inner - scanWidth - Scale(12), rowHeight);
            _status.Visible = !_nothingFound.Visible;
            return rowHeight + 2 * cardPadding;
        }

        private void LayoutChoices(int width)
        {
            int cardPadding = Scale(CardPadding);
            int inner = width - 2 * cardPadding;
            int cardY = cardPadding;
            _status.Visible = true;
            LayoutToggleRow(_selectAllRow, _selectAll, cardPadding, inner, ref cardY);
            cardY += Scale(10);
            Stack(_divider, cardPadding, inner, 1, Scale(10), ref cardY);

            int buttonHeight = Scale(34);
            int footerTop = _card.Height - cardPadding - buttonHeight;
            int removeWidth = Scale(150);
            int scanWidth = Scale(110);
            _remove.SetBounds(cardPadding + inner - removeWidth, footerTop, removeWidth, buttonHeight);
            _scan.SetBounds(_remove.Left - Scale(8) - scanWidth, footerTop, scanWidth, buttonHeight);
            int backupWidth = Scale(120);
            _openBackup.SetBounds(_scan.Left - Scale(8) - backupWidth, footerTop, backupWidth, buttonHeight);
            int statusRight = _openBackup.Visible ? _openBackup.Left : _scan.Left;
            _status.SetBounds(cardPadding, footerTop, statusRight - Scale(12) - cardPadding, buttonHeight);
            int restorePointTop = footerTop - Scale(12) - Scale(38);
            int restorePointY = restorePointTop;
            LayoutToggleRow(_restorePointRow, _restorePoint, cardPadding, inner, ref restorePointY);
            _footerDivider.SetBounds(cardPadding, restorePointTop - Scale(10), inner, 1);
            int listHeight = restorePointTop - Scale(20) - cardY;
            _viewport.SetBounds(cardPadding, cardY, inner, listHeight);
            LayoutList(inner, listHeight);
        }

        // The rows take the full width until they overflow, then make room for the scrollbar.
        private void LayoutList(int width, int visibleHeight)
        {
            List<IGrouping<Group, Entry>> groups = _entries.GroupBy(entry => entry.Group).ToList();
            int contentHeight = groups.Count * Scale(HeadingHeight) + _entries.Count * Scale(RowHeight + RowGap);
            int rowWidth = contentHeight > visibleHeight ? width - SystemInformation.VerticalScrollBarWidth : width;
            int y = 0;
            foreach (KeyValuePair<Group, MutedText> heading in _headings)
            {
                IGrouping<Group, Entry> group = groups.FirstOrDefault(candidate => candidate.Key == heading.Key);
                heading.Value.Visible = group != null;
                if (group == null)
                {
                    continue;
                }
                Stack(heading.Value, 0, rowWidth, Scale(HeadingHeight), 0, ref y);
                foreach (Entry entry in group)
                {
                    Stack(entry.Row, 0, rowWidth, Scale(RowHeight), Scale(RowGap), ref y);
                }
            }
            _list.SetBounds(0, _list.Top, rowWidth, y);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                ScanOnce();
            }
        }

        private void Scan()
        {
            ShowStage(Stage.Scanning);
            ContentChanged();
            Host.Backend.ScanMsiSoftware(Owner, (components, error) =>
            {
                _scanFailed = error != null;
                ShowComponents(error == null ? components : new MsiComponent[0]);
                ShowStage(Stage.Choosing);
                if (_scanFailed)
                {
                    _status.Text = "Couldn't look for MSI software: " + ExceptionText.Of(error);
                }
                ContentChanged();
                SoftwareCounted?.Invoke(_entries.Count);
            });
        }

        private void ShowComponents(IReadOnlyList<MsiComponent> components)
        {
            foreach (Entry entry in _entries)
            {
                entry.Row.Dispose();
            }
            _entries.Clear();
            foreach (MsiComponent component in components.OrderBy(component => GroupOf(component.Kind)))
            {
                var row = new SelectableRow(Theme) { Selected = component.SelectedByDefault };
                row.SetText(component.Name, KindLabel(component.Kind) + " · " + component.Location);
                row.SelectionChanged += OnSelectionChanged;
                _list.Controls.Add(row);
                _entries.Add(new Entry(component, row));
            }
            ContentChanged();
        }

        private void SelectAll(bool selected)
        {
            foreach (Entry entry in _entries)
            {
                entry.Row.Selected = selected;
            }
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            if (_stage == Stage.Confirming)
            {
                ShowStage(Stage.Choosing);
            }
            _selectAll.Checked = _entries.Count > 0 && _entries.All(entry => entry.Row.Selected);
            _remove.Inactive = SelectedEntries.Count == 0;
        }

        private void OnRemoveClicked()
        {
            if (_stage == Stage.Choosing && SelectedEntries.Count > 0)
            {
                ShowStage(Stage.Confirming);
            }
            else if (_stage == Stage.Confirming)
            {
                Remove(SelectedEntries);
            }
        }

        private void Remove(IReadOnlyList<Entry> selected)
        {
            ShowStage(Stage.Removing);
            bool makeRestorePoint = _restorePoint.Checked;
            _status.Text = makeRestorePoint ? "Making a restore point…" : "Getting ready: closing MSI's apps…";
            foreach (Entry entry in selected)
            {
                entry.Row.SetText(entry.Component.Name, "In line to be removed");
            }
            Host.Backend.RemoveMsiSoftware(Owner, selected.Select(entry => entry.Component).ToList(), makeRestorePoint,
                new RowProgress(selected, _status),
                (report, error) =>
                {
                    _backupFolder = report?.BackupFolder;
                    ShowStage(Stage.Finished);
                    _status.Text = error != null ? "Nothing was removed. " + ExceptionText.Of(error) : Summary(report);
                    int left = error != null ? _entries.Count : _entries.Count - selected.Count + report.FailedCount;
                    SoftwareCounted?.Invoke(left);
                });
        }

        // Results are delivered through the window: a page that has never been shown has no handle to deliver them to.
        private Control Owner => FindForm() ?? (Control)this;

        private IReadOnlyList<Entry> SelectedEntries => _entries.Where(entry => entry.Row.Selected).ToList();

        private void ShowStage(Stage stage)
        {
            _stage = stage;
            bool busy = stage == Stage.Scanning || stage == Stage.Removing;
            bool choosing = stage == Stage.Choosing || stage == Stage.Confirming;
            _scan.Inactive = busy;
            _remove.Inactive = !choosing || SelectedEntries.Count == 0;
            _remove.Text = stage == Stage.Confirming ? ConfirmLabel : RemoveLabel;
            bool showBackup = stage == Stage.Finished && _backupFolder != null;
            if (_openBackup.Visible != showBackup)
            {
                _openBackup.Visible = showBackup;
                ContentChanged();
            }
            _selectAll.Enabled = choosing && _entries.Count > 0;
            _selectAll.Checked = _entries.Count > 0 && _entries.All(entry => entry.Row.Selected);
            foreach (Entry entry in _entries)
            {
                entry.Row.Enabled = choosing;
            }
            switch (stage)
            {
                case Stage.Scanning:
                    _status.Text = "Looking for MSI software…";
                    break;
                case Stage.Choosing:
                    _status.Text = _entries.Count == 0 ? "No MSI software found on this PC." : _entries.Count + " items found. Switched-on items will be removed.";
                    break;
                case Stage.Confirming:
                    _status.Text = "Removes " + SelectedEntries.Count + " items. Registry keys are backed up first.";
                    break;
            }
        }

        private static string Summary(RemovalReport report)
        {
            int removed = report.Results.Count - report.FailedCount;
            string summary = "Removed " + removed + " of " + report.Results.Count + ".";
            if (report.FailedCount > 0)
            {
                summary += " " + report.FailedCount + " couldn't be removed.";
            }
            if (report.RestorePoint == RestorePointStatus.Failed)
            {
                summary += " No restore point: System Restore is off.";
            }
            return report.RestartNeeded ? summary + " Restart Windows to finish." : summary;
        }

        /// <summary>Shows on each row where the removal has got to, and the overall count below the list.</summary>
        private sealed class RowProgress : IRemovalProgress
        {
            private readonly IReadOnlyList<Entry> _entries;
            private readonly MutedText _status;
            private int _started;

            public RowProgress(IReadOnlyList<Entry> entries, MutedText status)
            {
                _entries = entries;
                _status = status;
            }

            public void Starting(MsiComponent component)
            {
                _started++;
                _status.Text = "Removing " + _started + " of " + _entries.Count + ": " + component.Name;
                Show(component, "Removing…");
            }

            public void Finished(RemovalResult result) => Show(result.Component, OutcomeText(result));

            private void Show(MsiComponent component, string detail)
            {
                Entry entry = _entries.First(candidate => candidate.Component == component);
                entry.Row.SetText(component.Name, detail);
            }
        }

        private static string OutcomeText(RemovalResult result)
        {
            switch (result.Outcome)
            {
                case RemovalOutcome.Removed:
                    return "Removed";
                case RemovalOutcome.RemovedAtRestart:
                    return "Removed. Finishes when Windows restarts";
                case RemovalOutcome.Failed:
                    return "Couldn't remove: " + result.Error;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown removal outcome.");
            }
        }

        private static Group GroupOf(MsiComponentKind kind)
        {
            switch (kind)
            {
                case MsiComponentKind.DriverPackage:
                case MsiComponentKind.StoreApp:
                case MsiComponentKind.Program:
                    return Group.Apps;
                case MsiComponentKind.Service:
                case MsiComponentKind.Driver:
                    return Group.ServicesAndDrivers;
                default:
                    return Group.Leftovers;
            }
        }

        private static string HeadingOf(Group group)
        {
            switch (group)
            {
                case Group.Apps:
                    return "APPS";
                case Group.ServicesAndDrivers:
                    return "SERVICES AND DRIVERS";
                case Group.Leftovers:
                    return "LEFTOVERS";
                default:
                    throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown group.");
            }
        }

        private static string KindLabel(MsiComponentKind kind)
        {
            switch (kind)
            {
                case MsiComponentKind.DriverPackage:
                    return "Installs MSI apps through Windows Update";
                case MsiComponentKind.StoreApp:
                    return "Store app";
                case MsiComponentKind.Program:
                    return "Program";
                case MsiComponentKind.Service:
                    return "Service";
                case MsiComponentKind.Driver:
                    return "Driver";
                case MsiComponentKind.ScheduledTask:
                    return "Scheduled task";
                case MsiComponentKind.StartupEntry:
                    return "Starts with Windows";
                case MsiComponentKind.Settings:
                    return "Registry";
                case MsiComponentKind.Folder:
                    return "Folder";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown MSI component kind.");
            }
        }
    }
}
