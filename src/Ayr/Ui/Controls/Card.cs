using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class Card : Panel
    {
        private const float CornerRadius = 8;

        private readonly Theme _theme;

        public Card(Theme theme)
        {
            _theme = theme;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = theme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(_theme.Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            RoundedRectangle.Fill(e.Graphics, bounds, CornerRadius, _theme.Surface);
            RoundedRectangle.Outline(e.Graphics, bounds, CornerRadius, _theme.Border);
        }
    }
}
