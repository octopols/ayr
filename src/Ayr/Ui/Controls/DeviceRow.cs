using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>One Apple device: its name and state, a green badge while fast charging, and a Send button when sending by hand.</summary>
    public sealed class DeviceRow : ThemedControl
    {
        private const string FastChargingLabel = "Fast charging";
        private const int Gap = 8;
        private const int BadgePadding = 8;
        private const float BadgeRadius = 4;

        // A lightning bolt on a unit square.
        private static readonly PointF[] Bolt =
        {
            new PointF(0.62f, 0f), new PointF(0.12f, 0.56f), new PointF(0.46f, 0.56f),
            new PointF(0.36f, 1f), new PointF(0.88f, 0.42f), new PointF(0.54f, 0.42f)
        };

        private readonly FlatButton _send;
        private readonly ToolTip _toolTip;
        private DeviceRowContent _content = DeviceRowContent.NoDevice;

        public DeviceRow(Theme theme, ToolTip toolTip) : base(theme)
        {
            BackColor = theme.Surface;
            _toolTip = toolTip;
            _send = new FlatButton(theme, DeviceRowContent.SendLabel, ButtonStyle.Secondary) { BackColor = theme.Surface, Visible = false };
            _send.Click += (sender, e) => SendClicked?.Invoke();
            Controls.Add(_send);
        }

        public event Action SendClicked;

        public void SetContent(DeviceRowContent content)
        {
            _content = content;
            _send.Visible = content.Send != SendButton.Hidden;
            _send.Text = content.SendText ?? "";
            _send.Inactive = content.Send == SendButton.Blocked;
            _toolTip.SetToolTip(_send, content.SendBlockedReason);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int height = Height * 4 / 5;
            int width = Math.Max(Measure(DeviceRowContent.SendLabel, Theme.BodyBold), Measure(DeviceRowContent.SentLabel, Theme.BodyBold)) + height;
            _send.SetBounds(Width - width - 1, (Height - height) / 2, width, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            int right = _send.Visible ? _send.Left - Gap : Width;
            if (_content.FastCharging)
            {
                right = DrawBadge(graphics, right) - Gap;
            }
            TextRenderer.DrawText(graphics, _content.Name, Theme.BodyBold, new Rectangle(0, 2, right, Theme.BodyBold.Height + 2), Theme.Text, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(graphics, _content.Detail, Theme.Small, new Rectangle(0, Theme.BodyBold.Height + 4, right, Theme.Small.Height + 2), Theme.TextMuted, TextFormatFlags.EndEllipsis);
        }

        /// <summary>Draws the badge ending at the given x and returns its left edge.</summary>
        private int DrawBadge(Graphics graphics, int right)
        {
            int height = Theme.Small.Height + BadgePadding;
            int boltSize = height - BadgePadding;
            int width = BadgePadding + boltSize + BadgePadding / 2 + Measure(FastChargingLabel, Theme.Small) + BadgePadding;
            var badge = new RectangleF(right - width, (Height - height) / 2f, width, height);
            RoundedRectangle.Fill(graphics, badge, BadgeRadius, Theme.PositiveBackground);
            using (var brush = new SolidBrush(Theme.Positive))
            {
                graphics.FillPolygon(brush, Array.ConvertAll(Bolt, point =>
                    new PointF(badge.X + BadgePadding + point.X * boltSize, badge.Y + BadgePadding / 2f + point.Y * boltSize)));
            }
            var label = new Rectangle((int)badge.X + BadgePadding + boltSize + BadgePadding / 2, (int)badge.Y, width, height);
            TextRenderer.DrawText(graphics, FastChargingLabel, Theme.Small, label, Theme.Positive, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return (int)badge.X;
        }

        private static int Measure(string text, Font font) => TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding).Width;
    }
}
