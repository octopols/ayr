using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class DeviceRow : ThemedControl
    {
        private const string ConnectedLabel = "Connected";
        private const int PillHeight = 20;
        private const int PillPadding = 13;
        private const int PillGap = 8;
        private const float PillRadius = 4;

        private string _name = "";
        private string _detail = "";
        private bool _connected;

        public DeviceRow(Theme theme) : base(theme)
        {
            BackColor = theme.Surface;
        }

        public void SetDevice(string name, string detail, bool connected)
        {
            _name = name;
            _detail = detail;
            _connected = connected;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            int textWidth = _connected ? DrawPill(graphics) - PillGap : Width;
            TextRenderer.DrawText(graphics, _name, Theme.BodyBold, new Rectangle(0, 2, textWidth, Theme.BodyBold.Height + 2), Theme.Text, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(graphics, _detail, Theme.Small, new Rectangle(0, Theme.BodyBold.Height + 4, textWidth, Theme.Small.Height + 2), Theme.TextMuted, TextFormatFlags.EndEllipsis);
        }

        /// <summary>Draws the pill at the right edge and returns its left edge.</summary>
        private int DrawPill(Graphics graphics)
        {
            int width = TextRenderer.MeasureText(ConnectedLabel, Theme.Small).Width + PillPadding;
            var pill = new RectangleF(Width - width - 1, (Height - PillHeight) / 2f, width, PillHeight);
            RoundedRectangle.Fill(graphics, pill, PillRadius, Theme.Accent);
            TextRenderer.DrawText(graphics, ConnectedLabel, Theme.Small, Rectangle.Round(pill), Theme.AccentText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return (int)pill.X;
        }
    }
}
