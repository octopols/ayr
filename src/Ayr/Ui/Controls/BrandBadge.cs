using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>Accent tile with the white Ayr mark, loaded at the size the tile needs.</summary>
    public sealed class BrandBadge : ThemedControl
    {
        private const float MarkRatio = 0.72f;
        private const float CornerRatio = 0.2f;

        private Image _mark;

        public BrandBadge(Theme theme) : base(theme)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(BackColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var tile = new RectangleF(0, 0, Width - 1, Height - 1);
            RoundedRectangle.Fill(graphics, tile, tile.Width * CornerRatio, Theme.Accent);
            Image mark = Mark();
            float size = Math.Min(mark.Width, tile.Width * MarkRatio);
            graphics.DrawImage(mark, new RectangleF((tile.Width - size) / 2, (tile.Height - size) / 2, size, size));
        }

        protected override void OnResize(EventArgs e)
        {
            _mark?.Dispose();
            _mark = null;
            base.OnResize(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _mark?.Dispose();
            }
            base.Dispose(disposing);
        }

        private Image Mark() => _mark ?? (_mark = Brand.LoadMark((int)(Height * MarkRatio)));
    }
}
