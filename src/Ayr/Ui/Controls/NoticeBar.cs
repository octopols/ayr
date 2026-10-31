using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class NoticeBar : ThemedControl
    {
        private const float CornerRadius = 6;
        private const int TextPadding = 14;

        public NoticeBar(Theme theme) : base(theme)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            RoundedRectangle.Fill(graphics, bounds, CornerRadius, Theme.NoticeBackground);
            RoundedRectangle.Outline(graphics, bounds, CornerRadius, Theme.NoticeBorder);
            var textBounds = new Rectangle(TextPadding, 0, Width - 2 * TextPadding, Height);
            TextRenderer.DrawText(graphics, Text, Theme.Body, textBounds, Theme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            base.OnTextChanged(e);
        }
    }
}
