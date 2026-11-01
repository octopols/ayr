using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        private sealed class Entry
        {
            public Entry(MsiComponent component, SelectableRow row)
            {
                Component = component;
                Row = row;
            }

            public MsiComponent Component { get; }
            public SelectableRow Row { get; }
            public MsiComponentGroup Group => MsiComponentGroups.Of(Component.Kind);
        }

        /// <summary>Shows on each row where the removal has got to, and the overall count below the list.</summary>
        private sealed class RowProgress : IRemovalProgress
        {
            private readonly IReadOnlyList<Entry> _entries;
            private readonly Action<string> _showStatus;
            private int _started;

            public RowProgress(IReadOnlyList<Entry> entries, Action<string> showStatus)
            {
                _entries = entries;
                _showStatus = showStatus;
            }

            public void Starting(MsiComponent component)
            {
                _started++;
                _showStatus(MsiRemovalText.Progress(_started, _entries.Count, component));
                Show(component, MsiRemovalText.Removing);
            }

            public void Finished(RemovalResult result) => Show(result.Component, MsiRemovalText.Outcome(result));

            private void Show(MsiComponent component, string detail) =>
                _entries.First(entry => entry.Component == component).Row.SetText(component.Name, detail);
        }

        private readonly TitleWithSubtitle _title;
        private readonly Card _card;
        private readonly TitleWithSubtitle _nothingFound;
        private readonly TitleWithSubtitle _selectAllRow;
        private readonly ToggleSwitch _selectAll;
        private readonly Panel _divider;
        private readonly ScrollViewport _viewport;
        private readonly Panel _list;
        private readonly Dictionary<MsiComponentGroup, MutedText> _headings = new Dictionary<MsiComponentGroup, MutedText>();
        private readonly Panel _footerDivider;
        private readonly TitleWithSubtitle _restorePointRow;
        private readonly ToggleSwitch _restorePoint;
        private readonly MutedText _status;
        private readonly FlatButton _openBackup;
        private readonly FlatButton _scan;
        private readonly FlatButton _remove;
        private readonly List<Entry> _entries = new List<Entry>();

        private Stage _stage = Stage.NotScanned;
        private bool _scanFailed;
        private bool _scanIncomplete;
        private string _backupFolder;

        public MsiSoftwarePage(Theme theme, IPageHost host) : base(theme, host)
        {
            _title = Add(SectionTitle("MSI software", "MSI's apps and services undo your settings in the background. Choose what to remove."));
            _card = Add(new Card(theme));
            _nothingFound = AddTo(_card, Row("No MSI software on this PC", "Nothing in the background is undoing your settings."));
            _selectAllRow = AddTo(_card, Row("Select all", null));
            _selectAll = AddTo(_card, SwitchFor(_selectAllRow));
            _selectAll.Toggled += SelectAll;
            _divider = AddTo(_card, new Panel { BackColor = theme.Border });
            _viewport = AddTo(_card, new ScrollViewport());
            _list = AddTo(_viewport, new Panel());
            foreach (MsiComponentGroup group in Enum.GetValues(typeof(MsiComponentGroup)))
            {
                _headings[group] = AddTo(_list, new MutedText(theme, MsiComponentGroups.Heading(group), theme.Surface));
            }
            _footerDivider = AddTo(_card, new Panel { BackColor = theme.Border });
            _restorePointRow = AddTo(_card, Row("Make a restore point first", "Takes longer, and only works if System Restore is turned on"));
            _restorePoint = AddTo(_card, SwitchFor(_restorePointRow));
            _restorePoint.Toggled += on => _restorePoint.Checked = on;
            _status = AddTo(_card, new MutedText(theme, "", theme.Surface) { Wraps = true });
            _openBackup = AddTo(_card, new FlatButton(theme, "Open backup", ButtonStyle.Secondary) { Visible = false });
            _openBackup.Click += (sender, e) => Process.Start("explorer.exe", "\"" + _backupFolder + "\"")?.Dispose();
            _scan = AddTo(_card, new FlatButton(theme, "Scan again", ButtonStyle.Secondary));
            _scan.Click += (sender, e) => Scan();
            _remove = AddTo(_card, new FlatButton(theme, RemoveLabel, ButtonStyle.Primary));
            _remove.Click += (sender, e) => OnRemoveClicked();
            ShowStage(Stage.NotScanned);
        }

        /// <summary>Raised after each scan or removal with how many items of MSI's are still on this PC.</summary>
        public event Action<int> SoftwareCounted;

        public override string Title => "MSI software";

        public override IEnumerable<Control> Inputs => new Control[] { _scan, _remove, _selectAll, _restorePoint };

        private bool HasItems => _entries.Count > 0;

        private bool AllSelected => HasItems && _entries.All(entry => entry.Row.Selected);

        private IReadOnlyList<Entry> SelectedEntries => _entries.Where(entry => entry.Row.Selected).ToList();

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
            foreach (Control choiceControl in new Control[] { _selectAllRow, _selectAll, _divider, _viewport, _footerDivider, _restorePointRow, _restorePoint, _remove })
            {
                choiceControl.Visible = HasItems;
            }
            _nothingFound.Visible = !HasItems && _stage != Stage.Scanning && !_scanFailed && !_scanIncomplete;
            _status.Visible = HasItems || !_nothingFound.Visible;
            if (HasItems)
            {
                _card.SetBounds(padding, y, width, Height - padding - y);
                LayoutChoices(width);
            }
            else
            {
                _card.SetBounds(padding, y, width, LayoutNothingFound(width));
            }
        }

        public override void InputsReleased() => EnableForStage();

        // Leaving the page disarms the confirmation, so coming back never finds removal one click away.
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                ScanOnce();
            }
            else if (_stage == Stage.Confirming)
            {
                ShowStage(Stage.Choosing);
            }
        }

        // With nothing to remove, the card says so and offers only a new scan.
        private int LayoutNothingFound(int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            int scanWidth = Scale(110);
            int rowHeight = Scale(38);
            int textWidth = inner - scanWidth - Scale(12);
            _nothingFound.SetBounds(padding, padding, textWidth, rowHeight);
            _status.SetBounds(padding, padding, textWidth, rowHeight);
            _scan.SetBounds(padding + inner - scanWidth, padding + Scale(2), scanWidth, Scale(34));
            return rowHeight + 2 * padding;
        }

        private void LayoutChoices(int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            int listTop = padding;
            LayoutToggleRow(_selectAllRow, _selectAll, padding, inner, ref listTop);
            listTop += Scale(10);
            Stack(_divider, padding, inner, 1, Scale(10), ref listTop);

            int footerTop = LayoutFooter(padding, inner);
            int restorePointTop = footerTop - Scale(12) - Scale(38);
            int restorePointBottom = restorePointTop;
            LayoutToggleRow(_restorePointRow, _restorePoint, padding, inner, ref restorePointBottom);
            _footerDivider.SetBounds(padding, restorePointTop - Scale(10), inner, 1);

            int listHeight = restorePointTop - Scale(20) - listTop;
            _viewport.SetBounds(padding, listTop, inner, listHeight);
            LayoutList(inner, listHeight);
        }

        /// <summary>
        /// Lays out the buttons along the bottom of the card with the status above them at full width, so a long summary
        /// (such as the restart reminder) is never cut off. Returns the status's top.
        /// </summary>
        private int LayoutFooter(int padding, int inner)
        {
            int buttonHeight = Scale(34);
            int buttonsTop = _card.Height - padding - buttonHeight;
            int gap = Scale(8);
            _remove.SetBounds(padding + inner - Scale(150), buttonsTop, Scale(150), buttonHeight);
            _scan.SetBounds(_remove.Left - gap - Scale(110), buttonsTop, Scale(110), buttonHeight);
            _openBackup.SetBounds(_scan.Left - gap - Scale(120), buttonsTop, Scale(120), buttonHeight);
            int statusHeight = Math.Max(_status.WrappedHeight(inner), Theme.Small.Height);
            int statusTop = buttonsTop - Scale(10) - statusHeight;
            _status.SetBounds(padding, statusTop, inner, statusHeight);
            return statusTop;
        }

        // The rows take the full width until they overflow, then make room for the scrollbar.
        private void LayoutList(int width, int visibleHeight)
        {
            List<IGrouping<MsiComponentGroup, Entry>> groups = _entries.GroupBy(entry => entry.Group).ToList();
            int contentHeight = groups.Count * Scale(HeadingHeight) + _entries.Count * Scale(RowHeight + RowGap);
            int rowWidth = contentHeight > visibleHeight ? width - SystemInformation.VerticalScrollBarWidth : width;
            int y = 0;
            foreach (KeyValuePair<MsiComponentGroup, MutedText> heading in _headings)
            {
                IGrouping<MsiComponentGroup, Entry> group = groups.FirstOrDefault(candidate => candidate.Key == heading.Key);
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

        // A removal started earlier keeps running after its window closes, and a new scan waits for it.
        private void Scan()
        {
            ShowStage(Stage.Scanning);
            if (Host.Backend.RemovingMsiSoftware)
            {
                ShowStatus(MsiRemovalText.ScanningAfterRemoval);
            }
            ContentChanged();
            Host.Backend.ScanMsiSoftware(Window, (scan, error) =>
            {
                _scanFailed = error != null;
                _scanIncomplete = scan != null && scan.Incomplete;
                if (_scanFailed)
                {
                    ActivityLog.Write("MSI scan failed: " + ExceptionText.Of(error));
                }
                ShowComponents(_scanFailed ? Array.Empty<MsiComponent>() : scan.Components);
                ShowStage(Stage.Choosing);
                ContentChanged();
                SoftwareCounted?.Invoke(_entries.Count);
            });
        }

        private void ShowStatus(string text)
        {
            if (_status.Text != text)
            {
                _status.Text = text;
                ContentChanged();
            }
        }

        private void ShowComponents(IReadOnlyList<MsiComponent> components)
        {
            foreach (Entry entry in _entries)
            {
                entry.Row.Dispose();
            }
            _entries.Clear();
            foreach (MsiComponent component in components.OrderBy(component => MsiComponentGroups.Of(component.Kind)))
            {
                var row = new SelectableRow(Theme) { Selected = component.SelectedByDefault };
                row.SetText(component.Name, MsiRemovalText.Detail(component));
                row.SelectionChanged += OnSelectionChanged;
                _list.Controls.Add(row);
                _entries.Add(new Entry(component, row));
            }
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
            _selectAll.Checked = AllSelected;
            _remove.Inactive = SelectedEntries.Count == 0;
            ShowStatus(ChoosingStatus());
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
            ShowStatus(makeRestorePoint ? "Making a restore point…" : "Getting ready: closing MSI's apps…");
            foreach (Entry entry in selected)
            {
                entry.Row.SetText(entry.Component.Name, MsiRemovalText.Queued);
            }
            Host.Backend.RemoveMsiSoftware(Window, selected.Select(entry => entry.Component).ToList(), makeRestorePoint,
                new RowProgress(selected, ShowStatus),
                (report, error) =>
                {
                    if (error != null)
                    {
                        OnRemovalNotStarted(selected);
                        return;
                    }
                    _backupFolder = HasFiles(report.BackupFolder) ? report.BackupFolder : null;
                    ShowStage(Stage.Finished);
                    ShowStatus(MsiRemovalText.Summary(report));
                    SoftwareCounted?.Invoke(_entries.Count - (report.Results.Count - report.FailedCount));
                });
        }

        // Nothing ran, so the rows go back to what they were and the user can try again.
        private void OnRemovalNotStarted(IReadOnlyList<Entry> selected)
        {
            foreach (Entry entry in selected)
            {
                entry.Row.SetText(entry.Component.Name, MsiRemovalText.Detail(entry.Component));
            }
            ShowStage(Stage.Choosing);
            ShowStatus(MsiRemovalText.PrepareFailed);
        }

        private static bool HasFiles(string folder) =>
            folder != null && Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any();

        private void ShowStage(Stage stage)
        {
            _stage = stage;
            _remove.Text = stage == Stage.Confirming ? ConfirmLabel : RemoveLabel;
            _selectAll.Checked = AllSelected;
            EnableForStage();
            bool showBackup = stage == Stage.Finished && _backupFolder != null;
            if (_openBackup.Visible != showBackup)
            {
                _openBackup.Visible = showBackup;
                ContentChanged();
            }
            switch (stage)
            {
                case Stage.Scanning:
                    ShowStatus(MsiRemovalText.Scanning);
                    break;
                case Stage.Choosing:
                    ShowStatus(ChoosingStatus());
                    break;
                case Stage.Confirming:
                    ShowStatus(MsiRemovalText.Confirmation(SelectedEntries.Select(entry => entry.Component).ToList()));
                    break;
            }
        }

        // The window enables every input after any change; this puts back what the current stage allows.
        private void EnableForStage()
        {
            bool busy = _stage == Stage.Scanning || _stage == Stage.Removing;
            bool choosing = _stage == Stage.Choosing || _stage == Stage.Confirming;
            _scan.Inactive = busy;
            _remove.Inactive = !choosing || SelectedEntries.Count == 0;
            _selectAll.Enabled = choosing && HasItems;
            _restorePoint.Enabled = choosing;
            foreach (Entry entry in _entries)
            {
                entry.Row.Enabled = choosing;
            }
        }

        private string ChoosingStatus()
        {
            if (_scanFailed)
            {
                return MsiRemovalText.ScanFailed;
            }
            if (!HasItems)
            {
                return _scanIncomplete ? MsiRemovalText.Found(0, incomplete: true) : "";
            }
            return SelectedEntries.Count == 0 ? MsiRemovalText.NothingSelected : MsiRemovalText.Found(_entries.Count, _scanIncomplete);
        }
    }
}
