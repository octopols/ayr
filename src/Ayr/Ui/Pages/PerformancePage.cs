using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Ayr.Controllers;
using Ayr.Ui.Controls;

namespace Ayr.Ui.Pages
{
    /// <summary>
    /// The everyday settings, top to bottom in the order people think about them: how fast, how loud, and the GPU overclock
    /// that only Turbo uses. Each choice says in one line what it does, so nobody has to guess what "Medium" means.
    /// </summary>
    public sealed class PerformancePage : WindowPage
    {
        private const string ApplyLabel = "Apply";
        private const string SavedLabel = "Saved";

        private static readonly Dictionary<PerformanceLevel, string> LevelMeanings = new Dictionary<PerformanceLevel, string>
        {
            [PerformanceLevel.Turbo] = "Highest power for CPU and GPU, plus any GPU overclock. Hottest and loudest.",
            [PerformanceLevel.High] = "High power at stock GPU speeds. The usual choice when plugged in.",
            [PerformanceLevel.Medium] = "Lower power limits. Cooler and quieter for browsing and work.",
            [PerformanceLevel.Low] = "Lowest power. Coolest and quietest, and longest on battery."
        };

        private static readonly Dictionary<FanMode, string> FanMeanings = new Dictionary<FanMode, string>
        {
            [FanMode.Auto] = "MSI's own curve: fans speed up as the laptop warms up.",
            [FanMode.Max] = "Both fans at their normal top speed all the time.",
            [FanMode.Silent] = "Slower, quieter fans. Switches back to Auto at 90°C or when Ayr exits.",
            [FanMode.Boost] = "Fans beyond their normal top speed. Loudest.",
            [FanMode.Custom] = "Your own curve for each fan."
        };

        private readonly TitleWithSubtitle _performanceTitle;
        private readonly Card _performanceCard;
        private readonly Segmented _performance;
        private readonly MutedText _performanceMeaning;
        private readonly TitleWithSubtitle _fanTitle;
        private readonly Card _fanCard;
        private readonly Segmented _fan;
        private readonly MutedText _fanMeaning;
        private readonly FlatButton _editCurve;
        private readonly TitleWithSubtitle _overclockTitle;
        private readonly Card _overclockCard;
        private readonly LabeledSlider _coreOffset, _memoryOffset;
        private readonly FlatButton _applyOverclock;

        private readonly MutedText _overclockUnavailable;

        private SlowState _slow;
        private PerformanceLevel? _livePerformance;
        private bool _customFanSelected;
        private bool _offsetsShown;
        private bool _openCurveOnceCustom;
        private bool _overclockAvailable = true;

        public PerformancePage(Theme theme, IPageHost host) : base(theme, host)
        {
            _performanceTitle = Add(SectionTitle("Performance", null));
            _performanceCard = Add(new Card(theme));
            _performance = AddTo(_performanceCard, Choices("Performance", PerformanceController.SelectableLevels));
            _performance.Picked += index => host.Apply(() => host.Backend.ApplyPerformance(PerformanceController.SelectableLevels[index]));
            _performanceMeaning = AddTo(_performanceCard, new MutedText(theme, "", theme.Surface));

            _fanTitle = Add(SectionTitle("Fans", null));
            _fanCard = Add(new Card(theme));
            _fan = AddTo(_fanCard, Choices("Fans", FanController.SelectableModes));
            _fan.Picked += OnFanPicked;
            _fanMeaning = AddTo(_fanCard, new MutedText(theme, "", theme.Surface));
            _editCurve = AddTo(_fanCard, new FlatButton(theme, "Edit curve…", ButtonStyle.Secondary) { Visible = false, Inactive = true });
            _editCurve.Click += (sender, e) => OpenCurveEditor();

            _overclockTitle = Add(SectionTitle("GPU overclock", null));
            _overclockCard = Add(new Card(theme));
            _coreOffset = AddTo(_overclockCard, new LabeledSlider(theme, "Core clock", 0, GpuOffsets.MaxCoreMhz, Megahertz));
            _memoryOffset = AddTo(_overclockCard, new LabeledSlider(theme, "Memory clock", 0, GpuOffsets.MaxMemoryMhz, Megahertz));
            _applyOverclock = AddTo(_overclockCard, new FlatButton(theme, ApplyLabel, ButtonStyle.Primary) { Inactive = true });
            _overclockUnavailable = AddTo(_overclockCard, new MutedText(theme, "Ayr can't overclock this laptop's graphics card.", theme.Surface)
            {
                Visible = false
            });
            _applyOverclock.Click += (sender, e) =>
            {
                GpuOffsets offsets = ChosenOffsets;
                host.Apply(() => host.Backend.ApplyTurboOffsets(offsets));
            };
            _coreOffset.ValueChanged += value => ShowOverclockState();
            _memoryOffset.ValueChanged += value => ShowOverclockState();
        }

        private GpuOffsets ChosenOffsets => new GpuOffsets(_coreOffset.Value, _memoryOffset.Value);

        public override string Title => "Performance";

        public override IEnumerable<Control> Inputs =>
            new Control[] { _performance, _fan, _editCurve, _coreOffset, _memoryOffset, _applyOverclock };

        public override void ShowLive(LiveState live)
        {
            _performance.SelectedIndex = PerformanceController.SelectableLevels.IndexOf(live.Performance);
            _performanceMeaning.Text = LevelMeanings.TryGetValue(live.Performance, out string level) ? level : "Set by another app.";
            _fan.SelectedIndex = FanController.SelectableModes.IndexOf(live.Fan);
            _fanMeaning.Text = FanMeanings.TryGetValue(live.Fan, out string fan) ? fan : "";
            _livePerformance = live.Performance;
            ShowOverclockSubtitle();
            bool custom = live.Fan == FanMode.Custom;
            if (custom != _customFanSelected)
            {
                _customFanSelected = custom;
                _editCurve.Visible = custom;
                ContentChanged();
            }
            if (custom && _openCurveOnceCustom)
            {
                _openCurveOnceCustom = false;
                OpenCurveEditor();
            }
        }

        public override void ShowSlow(SlowState slow)
        {
            _slow = slow;
            _editCurve.Inactive = false;
            if (slow.GpuOverclockAvailable != _overclockAvailable)
            {
                _overclockAvailable = slow.GpuOverclockAvailable;
                ContentChanged();
            }
            ShowOverclockSubtitle();
            if (!_offsetsShown)
            {
                // The sliders start at the saved offsets and are then left to the user until they press Apply.
                _coreOffset.Value = slow.Desired.TurboOffsets.CoreMhz;
                _memoryOffset.Value = slow.Desired.TurboOffsets.MemoryMhz;
                _offsetsShown = true;
            }
            ShowOverclockState();
        }

        private void ShowOverclockSubtitle()
        {
            if (!_overclockAvailable || _slow == null || !_livePerformance.HasValue)
            {
                _overclockTitle.SetSubtitle(null);
                return;
            }
            string now = !_slow.Desired.TurboOffsets.RaisesClocks ? "Set to +0, so nothing is added."
                : _livePerformance.Value == PerformanceLevel.Turbo ? "On right now."
                : "Off right now.";
            _overclockTitle.SetSubtitle("Added only in Turbo. " + now);
        }

        // The button only offers to apply when the sliders differ from what is saved, so its state answers "is this saved?".
        private void ShowOverclockState()
        {
            GpuOffsets saved = _slow?.Desired.TurboOffsets;
            if (saved == null)
            {
                return;
            }
            bool unsaved = saved.CoreMhz != _coreOffset.Value || saved.MemoryMhz != _memoryOffset.Value;
            _applyOverclock.Inactive = !unsaved;
            _applyOverclock.Text = unsaved ? ApplyLabel : SavedLabel;
        }

        public override void LayoutContent()
        {
            int padding = Scale(24);
            int width = Width - 2 * padding;
            int y = Scale(20);
            Stack(_performanceTitle, padding, width, Scale(24), Scale(8), ref y);
            Stack(_performanceCard, padding, width, LayoutPerformanceCard(width), Scale(16), ref y);
            Stack(_fanTitle, padding, width, Scale(24), Scale(8), ref y);
            Stack(_fanCard, padding, width, LayoutFanCard(width), Scale(16), ref y);
            Stack(_overclockTitle, padding, width, Scale(38), Scale(8), ref y);
            Stack(_overclockCard, padding, width, LayoutOverclockCard(width), 0, ref y);
        }

        private int LayoutPerformanceCard(int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            int y = padding;
            Stack(_performance, padding, inner, Scale(36), Scale(8), ref y);
            Stack(_performanceMeaning, padding, inner, Scale(28), 0, ref y);
            return y + Scale(8);
        }

        private int LayoutFanCard(int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            int y = padding;
            Stack(_fan, padding, inner, Scale(36), Scale(8), ref y);
            int buttonWidth = Scale(110);
            int meaningWidth = _customFanSelected ? inner - buttonWidth - Scale(12) : inner;
            _editCurve.SetBounds(padding + inner - buttonWidth, y, buttonWidth, Scale(30));
            Stack(_fanMeaning, padding, meaningWidth, Scale(28), 0, ref y);
            return y + Scale(8);
        }

        private int LayoutOverclockCard(int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            foreach (Control control in new Control[] { _coreOffset, _memoryOffset, _applyOverclock })
            {
                control.Visible = _overclockAvailable;
            }
            _overclockUnavailable.Visible = !_overclockAvailable;
            if (!_overclockAvailable)
            {
                _overclockUnavailable.SetBounds(padding, padding, inner, Scale(24));
                return Scale(24) + 2 * padding;
            }
            int applyWidth = Scale(96);
            int applyHeight = Scale(34);
            int gap = Scale(24);
            int sliderWidth = (inner - applyWidth - 2 * gap) / 2;
            int sliderHeight = Scale(44);
            _coreOffset.SetBounds(padding, padding, sliderWidth, sliderHeight);
            _memoryOffset.SetBounds(padding + sliderWidth + gap, padding, sliderWidth, sliderHeight);
            _applyOverclock.SetBounds(padding + inner - applyWidth, padding + sliderHeight - applyHeight, applyWidth, applyHeight);
            return sliderHeight + 2 * padding;
        }

        // Picking Custom opens the curve once the fans have switched to it: choosing it is almost always the start of shaping
        // it, and a switch that failed leaves its error in view instead of behind the editor.
        private void OnFanPicked(int index)
        {
            FanMode mode = FanController.SelectableModes[index];
            _openCurveOnceCustom = mode == FanMode.Custom;
            Host.Apply(() => Host.Backend.ApplyFan(mode));
        }

        private void OpenCurveEditor()
        {
            if (_slow == null)
            {
                return;
            }
            Form owner = FindForm();
            using (var window = new FanCurveWindow(Host.Backend, _slow.Desired.CustomCurves, _slow.CurveTemperatures))
            {
                window.CenterOver(owner);
                window.ShowDialog(owner);
            }
            Host.RefreshState();
        }

        private static string Megahertz(int value) => "+" + value + " MHz";
    }
}
