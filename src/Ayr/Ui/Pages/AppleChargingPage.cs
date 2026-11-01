using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Ayr.Apple;
using Ayr.Ui.Controls;

namespace Ayr.Ui.Pages
{
    /// <summary>
    /// Faster charging for iPhones and iPads plugged into the laptop. It is kept apart from the battery page on purpose:
    /// that page is about this laptop's battery, this one about other devices charging from its USB ports. While a row
    /// counts down, the page redraws it every second and reads the state again as soon as the wait is over, so the Send
    /// button turns on at that moment rather than at the window's next slow refresh.
    /// </summary>
    public sealed class AppleChargingPage : WindowPage
    {
        private const int MaxDeviceRows = 4;
        private static readonly TimeSpan CountdownInterval = TimeSpan.FromSeconds(1);

        private readonly TitleWithSubtitle _title;
        private readonly Card _card;
        private readonly TitleWithSubtitle _automaticRow;
        private readonly ToggleSwitch _toggle;
        private readonly Panel _divider;
        private readonly MutedText _devicesLabel;
        private readonly List<ListRow> _deviceRows = new List<ListRow>();
        private readonly Timer _countdown = new Timer { Interval = (int)CountdownInterval.TotalMilliseconds };

        private SlowState _slow;
        private int _visibleDeviceRows = 1;

        public AppleChargingPage(Theme theme, IPageHost host, ToolTip toolTip) : base(theme, host)
        {
            _title = Add(SectionTitle("iPhone & iPad charging", "Asks Apple devices on this laptop's USB ports to charge at their faster rate."));
            _card = Add(new Card(theme));
            _automaticRow = AddTo(_card, Row("Fast charge automatically", "Sent each time a device is plugged in"));
            _toggle = AddTo(_card, SwitchFor(_automaticRow));
            _toggle.Toggled += on => host.Apply(() => host.Backend.SetAppleAutoFastCharge(on));
            _divider = AddTo(_card, new Panel { BackColor = theme.Border });
            _devicesLabel = AddTo(_card, new MutedText(theme, "Connected now", theme.Surface));
            for (int row = 0; row < MaxDeviceRows; row++)
            {
                ListRow deviceRow = AddTo(_card, new ListRow(theme, toolTip, AppleRowContent.SendLabel, AppleRowContent.SentLabel));
                deviceRow.ButtonClicked += () => host.Apply(host.Backend.SendAppleFastCharge);
                _deviceRows.Add(deviceRow);
            }
            _countdown.Tick += (sender, e) => OnCountdownTick();
        }

        public override string Title => "iPhone & iPad";

        public override IEnumerable<Control> Inputs => new Control[] { _toggle }.Concat(_deviceRows);

        public override void ShowSlow(SlowState slow)
        {
            _slow = slow;
            _toggle.Checked = slow.Desired.AppleAutoFastCharge;
            ShowDevices(DateTime.Now);
            int visibleRows = Math.Max(1, Math.Min(MaxDeviceRows, slow.AppleDevices.Count));
            if (visibleRows != _visibleDeviceRows)
            {
                _visibleDeviceRows = visibleRows;
                ContentChanged();
            }
        }

        public override void LayoutContent()
        {
            int padding = Scale(24);
            int width = Width - 2 * padding;
            int y = padding;
            Stack(_title, padding, width, Scale(38), Scale(16), ref y);
            int cardPadding = Scale(CardPadding);
            int inner = width - 2 * cardPadding;
            int cardY = cardPadding;
            LayoutToggleRow(_automaticRow, _toggle, cardPadding, inner, ref cardY);
            cardY += Scale(14);
            Stack(_divider, cardPadding, inner, 1, Scale(12), ref cardY);
            Stack(_devicesLabel, cardPadding, inner, Scale(18), Scale(8), ref cardY);
            for (int row = 0; row < MaxDeviceRows; row++)
            {
                bool visible = row < _visibleDeviceRows;
                _deviceRows[row].Visible = visible;
                if (visible)
                {
                    Stack(_deviceRows[row], cardPadding, inner, Scale(40), Scale(6), ref cardY);
                }
            }
            _card.SetBounds(padding, y, width, cardY - Scale(6) + cardPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _countdown.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ShowDevices(DateTime now)
        {
            IReadOnlyList<AppleDeviceStatus> devices = _slow.AppleDevices;
            if (devices.Count == 0)
            {
                _deviceRows[0].SetContent(AppleRowContent.NoDevice);
            }
            for (int row = 0; row < Math.Min(devices.Count, MaxDeviceRows); row++)
            {
                _deviceRows[row].SetContent(AppleRowContent.For(devices[row], _slow.Desired.AppleAutoFastCharge, _slow.NextAppleRequestAllowedAt, now));
            }
            _countdown.Enabled = devices.Any(AppleRowContent.IsCountingDown);
        }

        // Once a wait has run out the shown state is stale; the countdown stops until fresh state arrives.
        private void OnCountdownTick()
        {
            DateTime now = DateTime.Now;
            if (_slow.AppleDevices.Any(status => AppleRowContent.CountdownEnded(status, _slow.NextAppleRequestAllowedAt, now)))
            {
                _countdown.Stop();
                Host.RefreshState();
                return;
            }
            ShowDevices(now);
        }
    }
}
