using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Ayr.Ui
{
    /// <summary>
    /// A small card at the top of the screen confirming a mode change. It never takes focus, so it is safe over a game,
    /// and hides itself after a moment. Only one exists at a time.
    /// </summary>
    public sealed class Osd : PopupWindow
    {
        private const int VisibleMs = 1600;
        private const int BaseWidth = 340;
        private const int BaseHeight = 64;
        private const int TopMargin = 16;
        private const float TileRatio = 0.64f;
        private const float TileRadius = 6;
        private const int TextRightMargin = 12;

        // Feather's "zap" outline on a 24-unit grid.
        private static readonly PointF[] Bolt =
        {
            new PointF(13, 2), new PointF(3, 14), new PointF(12, 14), new PointF(11, 22), new PointF(21, 10), new PointF(12, 10)
        };
        private const float BoltGrid = 24;

        private static Osd _current;

        private readonly ModeChange _change;
        private readonly Timer _hideTimer = new Timer { Interval = VisibleMs };

        private Osd(ModeChange change)
        {
            _change = change;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            Size = new Size(Scaled(BaseWidth), Scaled(BaseHeight));
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Top + Scaled(TopMargin));
            _hideTimer.Tick += (sender, e) => Close();
        }

        public static void Show(ModeChange change)
        {
            if (_current != null && !_current.IsDisposed)
            {
                _current.Close();
            }
            _current = new Osd(change);
            _current.Show();
            _current._hideTimer.Start();
        }

        protected override bool ShowWithoutActivation => true;

        // Topmost comes from the window style only: setting Form.TopMost repositions the window with a call that also
        // activates it, which would steal focus from the tray panel the user is working in.
        protected override int ExtendedWindowStyle => WindowStyles.ToolWindow | WindowStyles.NoActivate | WindowStyles.Topmost;

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Close();
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int tileSize = (int)(Height * TileRatio);
            float inset = (Height - tileSize) / 2f;
            var tile = new RectangleF(inset, inset, tileSize, tileSize);
            RoundedRectangle.Fill(graphics, tile, TileRadius, Theme.Accent);
            if (_change.Icon == OsdIcon.Fan)
            {
                DrawFan(graphics, tile);
            }
            else
            {
                DrawBolt(graphics, tile);
            }
            int textX = (int)(tile.Right + inset);
            TextRenderer.DrawText(graphics, _change.Title, Theme.Section, new Point(textX, (int)tile.Y), Theme.Text);
            var detailBounds = new Rectangle(textX, (int)tile.Bottom - Theme.Small.Height - 2, Width - textX - TextRightMargin, Theme.Small.Height + 4);
            TextRenderer.DrawText(graphics, _change.Detail, Theme.Small, detailBounds, Theme.TextMuted, TextFormatFlags.EndEllipsis);
            base.OnPaint(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _hideTimer.Stop();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hideTimer.Dispose();
                if (_current == this)
                {
                    _current = null;
                }
            }
            base.Dispose(disposing);
        }

        private void DrawFan(Graphics graphics, RectangleF tile)
        {
            float centerX = tile.X + tile.Width / 2;
            float centerY = tile.Y + tile.Height / 2;
            float blade = tile.Width * 0.30f;
            using (var pen = new Pen(Theme.AccentText, Math.Max(1.5f, tile.Width / 18f)))
            {
                for (int quarter = 0; quarter < 4; quarter++)
                {
                    GraphicsState state = graphics.Save();
                    graphics.TranslateTransform(centerX, centerY);
                    graphics.RotateTransform(quarter * 90);
                    graphics.DrawArc(pen, 0, -blade, blade, blade, 180, 180);
                    graphics.Restore(state);
                }
                graphics.DrawEllipse(pen, centerX - blade / 4, centerY - blade / 4, blade / 2, blade / 2);
            }
        }

        private void DrawBolt(Graphics graphics, RectangleF tile)
        {
            float scale = tile.Width * 0.6f / BoltGrid;
            float offset = tile.Width * 0.2f;
            PointF[] points = Bolt.Select(point => new PointF(tile.X + offset + point.X * scale, tile.Y + offset + point.Y * scale)).ToArray();
            using (var brush = new SolidBrush(Theme.AccentText))
            {
                graphics.FillPolygon(brush, points);
            }
        }
    }
}
