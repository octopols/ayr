using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Battery;
using Ayr.Controllers;
using Ayr.Ui.Controls;

namespace Ayr.Ui.Pages
{
    /// <summary>
    /// The laptop's own battery: how worn it is, where charging stops, and MSI's calibration, which empties and refills it
    /// once so its percentage is right again. Each appears only on laptops whose firmware offers it; calibration is turned
    /// down for a battery too worn to carry the laptop through it.
    /// </summary>
    public sealed class BatteryPage : WindowPage
    {
        private const int FullChargeIndex = 0;
        private const int CustomChargeIndex = 1;
        private const int HealthFactLines = 3;
        private const int FactLineHeight = 22;
        private const string ChargeLimitHint = "Mostly plugged in? Stopping at 80% slows wear.";
        private const string HeldAtFullHint = "Held at Full until the calibration ends";
        private const string CalibrateLabel = "Calibrate";
        private const string ConfirmLabel = "Start now";
        private const string CancelLabel = "Cancel";

        private readonly TitleWithSubtitle _title;
        private readonly Card _healthCard;
        private readonly TitleWithSubtitle _healthRow;
        private readonly FactList _healthFacts;
        private readonly Card _chargeCard;
        private readonly TitleWithSubtitle _chargeRow;
        private readonly Segmented _charge;
        private readonly LabeledSlider _chargeSlider;
        private readonly MutedText _unavailable;
        private readonly Card _calibrationCard;
        private readonly TitleWithSubtitle _calibrationRow;
        private readonly FlatButton _calibrate;
        private readonly MutedText _calibrationDetail;

        private bool _chargeLimitAvailable = true;
        private bool _chargeHeldForCalibration;
        private bool _custom;
        private int? _shownPercent;
        private BatteryHealth _health;
        private BatteryHistoryLookup _historyLookup = BatteryHistoryLookup.Checking;
        private bool _historyRequested;
        private CalibrationView _calibration;
        private bool _confirmingCalibration;

        public BatteryPage(Theme theme, IPageHost host) : base(theme, host)
        {
            _title = Add(SectionTitle("Battery", null));
            _healthCard = Add(new Card(theme) { Visible = false });
            _healthRow = AddTo(_healthCard, Row("Health", null));
            _healthFacts = AddTo(_healthCard, new FactList(theme));
            _chargeCard = Add(new Card(theme));
            _chargeRow = AddTo(_chargeCard, Row("Charge limit", ChargeLimitHint));
            _charge = AddTo(_chargeCard, new Segmented(theme, new[] { "Full", "Custom" }));
            _charge.Picked += index =>
            {
                int percent = index == FullChargeIndex ? ChargeLimitController.Full : _chargeSlider.Value;
                host.Apply(() => host.Backend.ApplyChargeLimit(percent));
            };
            _chargeSlider = AddTo(_chargeCard, new LabeledSlider(theme, "Stop charging at", ChargeLimitController.MinimumCustom, ChargeLimitController.MaximumCustom, Percent)
            {
                Value = ChargeLimitController.DefaultCustom
            });
            _chargeSlider.ValueCommitted += percent => host.Apply(() => host.Backend.ApplyChargeLimit(percent));
            _unavailable = Add(new MutedText(theme, "Ayr can't set this laptop's charge limit.", theme.Background) { Visible = false });

            _calibrationCard = Add(new Card(theme) { Visible = false });
            _calibrationRow = AddTo(_calibrationCard, Row("Battery calibration", null));
            _calibrate = AddTo(_calibrationCard, new FlatButton(theme, CalibrateLabel, ButtonStyle.Secondary));
            _calibrate.Click += (sender, e) => OnCalibrateClicked();
            _calibrationDetail = AddTo(_calibrationCard, new MutedText(theme, CalibrationText.Explanation, theme.Surface) { Wraps = true });
        }

        public override string Title => "Battery";

        public override IEnumerable<Control> Inputs => new Control[] { _charge, _chargeSlider, _calibrate };

        public override void ShowSlow(SlowState slow)
        {
            ShowHealth(slow.BatteryHealth);
            ShowChargeLimit(slow);
            ShowCalibration(slow.Calibration);
        }

        public override void LayoutContent()
        {
            int padding = Scale(24);
            int width = Width - 2 * padding;
            int y = padding;
            Stack(_title, padding, width, Scale(24), Scale(16), ref y);
            _healthCard.Visible = _health != null;
            if (_health != null)
            {
                Stack(_healthCard, padding, width, LayoutHealthCard(width), Scale(16), ref y);
            }
            _chargeCard.Visible = _chargeLimitAvailable;
            _unavailable.Visible = !_chargeLimitAvailable;
            if (_chargeLimitAvailable)
            {
                Stack(_chargeCard, padding, width, LayoutChargeCard(width), Scale(16), ref y);
            }
            else
            {
                Stack(_unavailable, padding, width, Scale(24), Scale(16), ref y);
            }
            _calibrationCard.Visible = _calibration != null;
            if (_calibration != null)
            {
                Stack(_calibrationCard, padding, width, LayoutCalibrationCard(width), 0, ref y);
            }
        }

        private int LayoutHealthCard(int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            int y = padding;
            Stack(_healthRow, padding, inner, Scale(38), Scale(4), ref y);
            Stack(_healthFacts, padding, inner, Scale(HealthFactLines * FactLineHeight), 0, ref y);
            return y + padding;
        }

        private int LayoutChargeCard(int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            int choiceWidth = Scale(220);
            int y = padding;
            _charge.Visible = !_chargeHeldForCalibration;
            int rowWidth = _charge.Visible ? inner - choiceWidth - Scale(16) : inner;
            _chargeRow.SetBounds(padding, y, rowWidth, Scale(38));
            _charge.SetBounds(padding + inner - choiceWidth, y + Scale(2), choiceWidth, Scale(34));
            y += Scale(38);
            _chargeSlider.Visible = _custom && !_chargeHeldForCalibration;
            if (_chargeSlider.Visible)
            {
                y += Scale(16);
                Stack(_chargeSlider, padding, inner, Scale(44), 0, ref y);
            }
            return y + padding;
        }

        private int LayoutCalibrationCard(int width)
        {
            int padding = Scale(CardPadding);
            int inner = width - 2 * padding;
            int buttonWidth = Scale(110);
            int y = padding;
            _calibrationRow.SetBounds(padding, y, inner - buttonWidth - Scale(16), Scale(38));
            _calibrate.SetBounds(padding + inner - buttonWidth, y + Scale(2), buttonWidth, Scale(34));
            y += Scale(38) + Scale(10);
            Stack(_calibrationDetail, padding, inner, _calibrationDetail.WrappedHeight(inner), 0, ref y);
            return y + padding;
        }

        // While a calibration holds the limit at Full, the choice is hidden rather than shown as something it isn't.
        private void ShowChargeLimit(SlowState slow)
        {
            bool calibrating = slow.Calibration != null && slow.Calibration.InProgress;
            _chargeRow.SetSubtitle(calibrating ? HeldAtFullHint : ChargeLimitSubtitle(slow.ChargeLimitPercent, slow.Battery));
            bool available = slow.ChargeLimitPercent.HasValue;
            if (available != _chargeLimitAvailable || calibrating != _chargeHeldForCalibration)
            {
                _chargeLimitAvailable = available;
                _chargeHeldForCalibration = calibrating;
                ContentChanged();
            }
            if (available && !calibrating)
            {
                ShowChargeChoice(slow.ChargeLimitPercent.Value, slow.Desired.CustomChargePercent);
            }
        }

        // A limit only stops charging; it never drains the battery, so a battery above it looks like the limit isn't working.
        private static string ChargeLimitSubtitle(int? limit, BatteryReading battery)
        {
            if (!limit.HasValue || battery == null || limit.Value == ChargeLimitController.Full || battery.Percent <= limit.Value)
            {
                return ChargeLimitHint;
            }
            return "At " + battery.Percent + "%, above your " + limit.Value + "% limit. It charges again below " + limit.Value + "%.";
        }

        // The slider shows the battery's own limit when one is set, otherwise the last custom value, ready for Custom.
        private void ShowChargeChoice(int limit, int lastCustomLimit)
        {
            if (limit == _shownPercent)
            {
                return;
            }
            _shownPercent = limit;
            bool custom = limit != ChargeLimitController.Full;
            _charge.SelectedIndex = custom ? CustomChargeIndex : FullChargeIndex;
            _chargeSlider.Value = custom ? limit : lastCustomLimit;
            if (custom != _custom)
            {
                _custom = custom;
                ContentChanged();
            }
        }

        private void ShowHealth(BatteryHealth health)
        {
            bool visibilityChanged = (health == null) != (_health == null);
            _health = health;
            if (health != null)
            {
                _healthRow.SetSubtitle(BatteryHealthText.Summary(health));
                _healthFacts.ShowFacts(BatteryHealthText.Facts(health, _historyLookup));
            }
            if (visibilityChanged)
            {
                ContentChanged();
            }
        }

        // Only this page shows the battery's history, so the report is read when it is opened, once per window. Leaving the
        // page disarms "Start now", so coming back never finds it one click from starting.
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
            {
                DisarmCalibration();
                return;
            }
            if (_historyRequested)
            {
                return;
            }
            _historyRequested = true;
            Host.Backend.ReadBatteryHistory(Window, OnHistoryRead);
        }

        private void OnHistoryRead(BatteryHistory history, Exception error)
        {
            if (error == null)
            {
                _historyLookup = BatteryHistoryLookup.Found(history);
            }
            else
            {
                ActivityLog.Write("battery history unavailable: " + ExceptionText.Of(error));
                _historyLookup = BatteryHistoryLookup.Failed;
            }
            ShowHealth(_health);
        }

        private void ShowCalibration(CalibrationView calibration)
        {
            bool layoutChanged = (calibration == null) != (_calibration == null);
            _calibration = calibration;
            if (calibration != null)
            {
                if (calibration.InProgress)
                {
                    _confirmingCalibration = false;
                }
                if (CannotStart(calibration))
                {
                    _confirmingCalibration = false;
                }
                _calibrationRow.SetSubtitle(CalibrationText.Summary(calibration));
                _calibrate.Text = CalibrateButtonLabel(calibration);
                _calibrate.Inactive = CannotStart(calibration);
                string detail = CalibrationDetail(calibration);
                _calibrate.AccessibleDescription = _calibrate.Inactive ? detail : null;
                layoutChanged |= detail != _calibrationDetail.Text;
                _calibrationDetail.Text = detail;
            }
            if (layoutChanged)
            {
                ContentChanged();
            }
        }

        private void DisarmCalibration()
        {
            if (_confirmingCalibration && _calibration != null)
            {
                _confirmingCalibration = false;
                ShowCalibration(_calibration);
            }
        }

        private bool CannotStart(CalibrationView calibration) =>
            !calibration.InProgress && (BatteryTooWornToCalibrate || calibration.ChargerUnplugged);

        private bool BatteryTooWornToCalibrate => _health != null && !_health.CanCalibrate;

        private string CalibrateButtonLabel(CalibrationView calibration)
        {
            if (calibration.InProgress)
            {
                return CancelLabel;
            }
            return _confirmingCalibration ? ConfirmLabel : CalibrateLabel;
        }

        private string CalibrationDetail(CalibrationView calibration)
        {
            if (calibration.InProgress)
            {
                return CalibrationText.WhileRunning;
            }
            if (BatteryTooWornToCalibrate)
            {
                return BatteryHealthText.CalibrationUnavailable(_health);
            }
            if (calibration.ChargerUnplugged)
            {
                return CalibrationText.PlugInCharger;
            }
            return _confirmingCalibration ? CalibrationText.Confirmation(_chargeLimitAvailable) : CalibrationText.Idle(calibration);
        }

        private void OnCalibrateClicked()
        {
            if (_calibration == null)
            {
                return;
            }
            if (_calibration.InProgress)
            {
                Host.Apply(Host.Backend.CancelCalibration);
            }
            else if (_confirmingCalibration)
            {
                _confirmingCalibration = false;
                Host.Apply(Host.Backend.StartCalibration);
            }
            else
            {
                _confirmingCalibration = true;
                ShowCalibration(_calibration);
            }
        }

        private static string Percent(int value) => value + "%";
    }
}
