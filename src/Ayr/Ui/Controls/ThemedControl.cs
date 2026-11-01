using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>
    /// Base for the owner-drawn controls: double-buffered and repainted only when their state changes. The background is
    /// left unset so it follows the parent, which keeps rounded corners blending into a card or the page.
    /// </summary>
    public abstract class ThemedControl : Control
    {
        protected ThemedControl(Theme theme)
        {
            Theme = theme;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            ForeColor = theme.Text;
            Font = theme.Body;
        }

        protected Theme Theme { get; }

        protected static void Smooth(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        protected void DrawFocus(Graphics graphics, RectangleF bounds)
        {
            if (!Focused || !ShowFocusCues)
            {
                return;
            }
            using (var pen = new Pen(Theme.Accent, 1.5f) { DashStyle = DashStyle.Dot })
            using (GraphicsPath path = RoundedRectangle.Create(RectangleF.Inflate(bounds, -1.5f, -1.5f), 6))
            {
                graphics.DrawPath(pen, path);
            }
        }

        protected static bool IsActivationKey(Keys key) => key == Keys.Space || key == Keys.Enter;
    }
}
