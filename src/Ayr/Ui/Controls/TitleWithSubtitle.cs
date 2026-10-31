using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class TitleWithSubtitle : ThemedControl
    {
        private readonly string _title;
        private readonly Font _titleFont;
        private string _subtitle;

        public TitleWithSubtitle(Theme theme, string title, string subtitle, Font titleFont, Color background) : base(theme)
        {
            _title = title;
            _subtitle = subtitle;
            _titleFont = titleFont;
            BackColor = background;
        }

        public void SetSubtitle(string subtitle)
        {
            if (_subtitle != subtitle)
            {
                _subtitle = subtitle;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, _title, _titleFont, Point.Empty, Theme.Text);
            if (string.IsNullOrEmpty(_subtitle))
            {
                return;
            }
            var subtitleBounds = new Rectangle(0, _titleFont.Height + 1, Width, Theme.Small.Height + 2);
            TextRenderer.DrawText(e.Graphics, _subtitle, Theme.Small, subtitleBounds, Theme.TextMuted, TextFormatFlags.EndEllipsis);
        }
    }
}
