using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class MutedText : ThemedControl
    {
        public MutedText(Theme theme, string text, Color background) : base(theme)
        {
            Text = text;
            BackColor = background;
        }

        /// <summary>Wraps onto further lines from the top instead of cutting off one centred line.</summary>
        public bool Wraps { get; set; }

        public int WrappedHeight(int width) =>
            TextRenderer.MeasureText(Text, Theme.Small, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextFormatFlags layout = Wraps
                ? TextFormatFlags.WordBreak
                : TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, Text, Theme.Small, ClientRectangle, Theme.TextMuted, layout | TextFormatFlags.NoPrefix);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            base.OnTextChanged(e);
        }
    }
}
