using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class MetricCell : ThemedControl
    {
        private const string Placeholder = "–";

        private readonly string _caption;
        private readonly Font _valueFont;
        private string _value = Placeholder;

        public MetricCell(Theme theme, string caption, Font valueFont) : base(theme)
        {
            _caption = caption;
            _valueFont = valueFont;
            BackColor = theme.Surface;
        }

        public void SetValue(string value)
        {
            if (_value != value)
            {
                _value = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, _caption, Theme.Small, Point.Empty, Theme.TextMuted);
            TextRenderer.DrawText(e.Graphics, _value, _valueFont, new Point(0, Theme.Small.Height + 2), Theme.Text);
        }
    }
}
