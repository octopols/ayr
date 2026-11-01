using System.Collections.Generic;
using System.Windows.Forms;
using Ayr.Controllers;
using Ayr.Ui.Controls;

namespace Ayr.Ui.Pages
{
    /// <summary>
    /// The laptop's own battery: charge to full, or stop at a chosen level. Only laptops whose firmware Ayr knows get the
    /// setting; elsewhere a short note says so.
    /// </summary>
    public sealed class BatteryPage : WindowPage
    {
        private const int FullChargeIndex = 0;
        private const int CustomChargeIndex = 1;

        private readonly TitleWithSubtitle _title;
        private readonly Card _card;
        private readonly TitleWithSubtitle _chargeRow;
        private readonly Segmented _charge;
        private readonly LabeledSlider _chargeSlider;
        private readonly MutedText _unavailable;

        private bool _available = true;
        private bool _custom;
        private int? _shownPercent;

        public BatteryPage(Theme theme, IPageHost host) : base(theme, host)
        {
            _title = Add(SectionTitle("Battery", "This laptop's own battery."));
            _card = Add(new Card(theme));
            _chargeRow = AddTo(_card, Row("Charge limit", "Mostly plugged in? Stopping at 80% slows wear."));
            _charge = AddTo(_card, new Segmented(theme, new[] { "Full", "Custom" }));
            _charge.Picked += index =>
            {
                int percent = index == FullChargeIndex ? ChargeLimitController.Full : _chargeSlider.Value;
                host.Apply(() => host.Backend.ApplyChargeLimit(percent));
            };
            _chargeSlider = AddTo(_card, new LabeledSlider(theme, "Stop charging at", ChargeLimitController.MinimumCustom, ChargeLimitController.MaximumCustom, Percent)
            {
                Value = ChargeLimitController.DefaultCustom
            });
            _chargeSlider.ValueCommitted += percent => host.Apply(() => host.Backend.ApplyChargeLimit(percent));
            _unavailable = Add(new MutedText(theme, "Charge limit isn't available for this laptop yet.", theme.Background) { Visible = false });
        }

        public override string Title => "Battery";

        public override IEnumerable<Control> Inputs => new Control[] { _charge, _chargeSlider };

        // The slider shows the battery's own limit when one is set, otherwise the last custom value, ready for Custom.
        public override void ShowSlow(SlowState slow)
        {
            bool available = slow.ChargeLimitPercent.HasValue;
            bool custom = available && slow.ChargeLimitPercent.Value != ChargeLimitController.Full;
            if (available && slow.ChargeLimitPercent != _shownPercent)
            {
                _shownPercent = slow.ChargeLimitPercent;
                _charge.SelectedIndex = custom ? CustomChargeIndex : FullChargeIndex;
                _chargeSlider.Value = custom ? slow.ChargeLimitPercent.Value : slow.Desired.CustomChargePercent;
            }
            if (available != _available || custom != _custom)
            {
                _available = available;
                _custom = custom;
                ContentChanged();
            }
        }

        public override void LayoutContent()
        {
            int padding = Scale(24);
            int width = Width - 2 * padding;
            int y = padding;
            Stack(_title, padding, width, Scale(38), Scale(16), ref y);
            _card.Visible = _available;
            _unavailable.Visible = !_available;
            if (!_available)
            {
                Stack(_unavailable, padding, width, Scale(24), 0, ref y);
                return;
            }
            int cardPadding = Scale(CardPadding);
            int inner = width - 2 * cardPadding;
            int cardY = cardPadding;
            int choiceWidth = Scale(220);
            _chargeRow.SetBounds(cardPadding, cardY, inner - choiceWidth - Scale(16), Scale(38));
            _charge.SetBounds(cardPadding + inner - choiceWidth, cardY + Scale(2), choiceWidth, Scale(34));
            cardY += Scale(38);
            _chargeSlider.Visible = _custom;
            if (_custom)
            {
                cardY += Scale(16);
                Stack(_chargeSlider, cardPadding, inner, Scale(44), 0, ref cardY);
            }
            _card.SetBounds(padding, y, width, cardY + cardPadding);
        }

        private static string Percent(int value) => value + "%";
    }
}
