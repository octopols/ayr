using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>A row in a card list: name and detail, a green badge for an active state, and one button on the right.</summary>
    public sealed class ListRow : ThemedControl
    {
        private const int Gap = 8;
        private const int BadgePadding = 8;
        private const float BadgeRadius = 4;

        // Icons drawn on a unit square.
        private static readonly PointF[] Bolt =
        {
            new PointF(0.62f, 0f), new PointF(0.12f, 0.56f), new PointF(0.46f, 0.56f),
            new PointF(0.36f, 1f), new PointF(0.88f, 0.42f), new PointF(0.54f, 0.42f)
        };
        private static readonly PointF[] Play = { new PointF(0.2f, 0.05f), new PointF(0.9f, 0.5f), new PointF(0.2f, 0.95f) };

        private readonly FlatButton _button;
        private readonly ToolTip _toolTip;
        private readonly string[] _buttonLabels;
        private RowContent _content = new RowContent("", "");

        /// <param name="buttonLabels">Every label the button can show; it is sized for the widest so buttons line up.</param>
        public ListRow(Theme theme, ToolTip toolTip, params string[] buttonLabels) : base(theme)
        {
            BackColor = theme.Surface;
            _toolTip = toolTip;
            _buttonLabels = buttonLabels;
            _button = new FlatButton(theme, "", ButtonStyle.Secondary) { BackColor = theme.Surface, Visible = false };
            _button.Click += (sender, e) => ButtonClicked?.Invoke();
            Controls.Add(_button);
        }

        public event Action ButtonClicked;

        public void SetContent(RowContent content)
        {
            _content = content;
            _button.Visible = content.Button != RowButton.Hidden;
            _button.Text = content.ButtonText ?? "";
            _button.Inactive = content.Button == RowButton.Blocked;
            _toolTip.SetToolTip(_button, content.BlockedReason);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int height = Height * 4 / 5;
            int width = _buttonLabels.Max(label => Measure(label, Theme.BodyBold)) + height;
            _button.SetBounds(Width - width - 1, (Height - height) / 2, width, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            int right = _button.Visible ? _button.Left - Gap : Width;
            if (_content.Badge != null)
            {
                right = DrawBadge(graphics, right) - Gap;
            }
            TextRenderer.DrawText(graphics, _content.Name, Theme.BodyBold, new Rectangle(0, 2, right, Theme.BodyBold.Height + 2), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(graphics, _content.Detail, Theme.Small, new Rectangle(0, Theme.BodyBold.Height + 4, right, Theme.Small.Height + 2), Theme.TextMuted, TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>Draws the badge ending at the given x and returns its left edge.</summary>
        private int DrawBadge(Graphics graphics, int right)
        {
            int height = Theme.Small.Height + BadgePadding;
            int iconSize = height - BadgePadding;
            int width = BadgePadding + iconSize + BadgePadding / 2 + Measure(_content.Badge, Theme.Small) + BadgePadding;
            var badge = new RectangleF(right - width, (Height - height) / 2f, width, height);
            RoundedRectangle.Fill(graphics, badge, BadgeRadius, Theme.PositiveBackground);
            PointF[] icon = _content.BadgeIcon == BadgeIcon.Play ? Play : Bolt;
            using (var brush = new SolidBrush(Theme.Positive))
            {
                graphics.FillPolygon(brush, Array.ConvertAll(icon, point =>
                    new PointF(badge.X + BadgePadding + point.X * iconSize, badge.Y + BadgePadding / 2f + point.Y * iconSize)));
            }
            var label = new Rectangle((int)badge.X + BadgePadding + iconSize + BadgePadding / 2, (int)badge.Y, width, height);
            TextRenderer.DrawText(graphics, _content.Badge, Theme.Small, label, Theme.Positive, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return (int)badge.X;
        }

        private static int Measure(string text, Font font) => TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding).Width;
    }
}
