using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>Names the two lines on the fan curve chart, right-aligned and drawn with the chart's own pens.</summary>
    public sealed class CurveLegend : ThemedControl
    {
        private const int SampleLength = 22;
        private const int SampleGap = 8;
        private const int EntryGap = 20;
        private const string CustomLabel = "Your curve";
        private const string AutoLabel = "Auto (MSI's curve)";

        public CurveLegend(Theme theme) : base(theme)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            int autoLeft = Width - EntryWidth(AutoLabel);
            int customLeft = autoLeft - EntryGap - EntryWidth(CustomLabel);
            using (Pen custom = FanCurveEditor.CreateCustomLinePen(Theme))
            using (Pen auto = FanCurveEditor.CreateAutoLinePen(Theme))
            {
                DrawEntry(graphics, customLeft, CustomLabel, custom);
                DrawEntry(graphics, autoLeft, AutoLabel, auto);
            }
        }

        private int EntryWidth(string label) => SampleLength + SampleGap + TextRenderer.MeasureText(label, Theme.Small).Width;

        private void DrawEntry(Graphics graphics, int left, string label, Pen pen)
        {
            float middle = Height / 2f;
            graphics.DrawLine(pen, left, middle, left + SampleLength, middle);
            var text = new Rectangle(left + SampleLength + SampleGap, 0, Width, Height);
            TextRenderer.DrawText(graphics, label, Theme.Small, text, Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
