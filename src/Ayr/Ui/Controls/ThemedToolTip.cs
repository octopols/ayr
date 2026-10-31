using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>A tooltip drawn in the app's dark palette rather than the system's light one.</summary>
    public sealed class ThemedToolTip : ToolTip
    {
        private const int TextPadding = 6;
        private const int MaxTextWidth = 260;
        private const TextFormatFlags Wrapping = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;

        private readonly Theme _theme;

        public ThemedToolTip(Theme theme)
        {
            _theme = theme;
            OwnerDraw = true;
            Popup += OnPopup;
            Draw += OnDraw;
        }

        private void OnPopup(object sender, PopupEventArgs e)
        {
            Size text = TextRenderer.MeasureText(GetToolTip(e.AssociatedControl), _theme.Small, new Size(MaxTextWidth, 0), Wrapping);
            e.ToolTipSize = new Size(text.Width + 2 * TextPadding, text.Height + 2 * TextPadding);
        }

        private void OnDraw(object sender, DrawToolTipEventArgs e)
        {
            using (var background = new SolidBrush(_theme.SurfaceAlt))
            using (var border = new Pen(_theme.Border))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
                e.Graphics.DrawRectangle(border, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }
            Rectangle text = Rectangle.Inflate(e.Bounds, -TextPadding, -TextPadding);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, _theme.Small, text, _theme.Text, Wrapping);
        }
    }
}
