using System.Drawing;
using System.Drawing.Drawing2D;

namespace Ayr.Ui
{
    public static class RoundedRectangle
    {
        public static GraphicsPath Create(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;
            if (diameter <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Fill(Graphics graphics, RectangleF bounds, float radius, Color color)
        {
            using (GraphicsPath path = Create(bounds, radius))
            using (var brush = new SolidBrush(color))
            {
                graphics.FillPath(brush, path);
            }
        }

        public static void Outline(Graphics graphics, RectangleF bounds, float radius, Color color)
        {
            using (GraphicsPath path = Create(bounds, radius))
            using (var pen = new Pen(color))
            {
                graphics.DrawPath(pen, path);
            }
        }
    }
}
