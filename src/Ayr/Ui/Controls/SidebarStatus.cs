using System.Drawing;
using System.Windows.Forms;
using Ayr.Hardware;

namespace Ayr.Ui.Controls
{
    /// <summary>
    /// Temperature and fan speed for the CPU and GPU, one line each, under a rule. Lines rather than tiles, because the
    /// sidebar is too narrow for four tiles to breathe.
    /// </summary>
    public sealed class SidebarStatus : ThemedControl
    {
        private const int RuleGap = 12;
        private const int LineCount = 2;
        private const string NameColumnSample = "GPU";
        private const int NameColumnGap = 6;

        private TelemetrySample _telemetry;

        public SidebarStatus(Theme theme) : base(theme)
        {
        }

        public int PreferredHeight(int lineHeight) => RuleGap + LineCount * lineHeight;

        public void ShowReading(TelemetrySample telemetry)
        {
            _telemetry = telemetry;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(BackColor);
            using (var rule = new Pen(Theme.Border))
            {
                graphics.DrawLine(rule, 0, 0, Width, 0);
            }
            if (_telemetry == null)
            {
                return;
            }
            int lineHeight = (Height - RuleGap) / LineCount;
            DrawLine(graphics, RuleGap, lineHeight, "CPU", _telemetry.CpuTemperature, _telemetry.CpuFanRpm);
            DrawLine(graphics, RuleGap + lineHeight, lineHeight, "GPU", _telemetry.GpuTemperature, _telemetry.GpuFanRpm);
        }

        private void DrawLine(Graphics graphics, int top, int height, string name, int temperature, int fanRpm)
        {
            var line = new Rectangle(0, top, Width, height);
            int nameWidth = TextRenderer.MeasureText(NameColumnSample, Theme.Small).Width + NameColumnGap;
            var value = new Rectangle(nameWidth, top, Width - nameWidth, height);
            TextRenderer.DrawText(graphics, name, Theme.Small, line, Theme.TextMuted, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(graphics, temperature + "°C", Theme.BodyBold, value, Theme.Text, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(graphics, fanRpm.ToString("N0") + " rpm", Theme.Small, line, Theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
    }
}
