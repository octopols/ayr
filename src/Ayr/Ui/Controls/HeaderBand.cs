using System;
using System.Drawing;
using System.Windows.Forms;
using Ayr.Interop;

namespace Ayr.Ui.Controls
{
    /// <summary>Title band for a borderless window. Dragging it moves the window.</summary>
    public sealed class HeaderBand : ThemedControl
    {
        private const int EdgeMargin = 16;
        private const int TextGap = 12;
        private const float BadgeHeightRatio = 0.6f;
        private const float ButtonHeightRatio = 0.5f;
        private const int ButtonSpacing = 4;
        private const int RightMargin = 8;
        private const float GlyphHalfSizeRatio = 0.18f;
        private const float CaptionButtonRadius = 6;

        private readonly BrandBadge _badge;
        private readonly string _title;
        private readonly string _subtitle;
        private readonly bool _canMinimize;
        private CaptionButton _hovered = CaptionButton.None;

        public HeaderBand(Theme theme, string title, string subtitle, bool canMinimize = true) : base(theme)
        {
            _canMinimize = canMinimize;
            _title = title;
            _subtitle = subtitle;
            _badge = new BrandBadge(theme);
            Controls.Add(_badge);
        }

        public event Action MinimizeClicked;
        public event Action CloseClicked;

        protected override void OnResize(EventArgs e)
        {
            int size = (int)(Height * BadgeHeightRatio);
            _badge.SetBounds(EdgeMargin, (Height - size) / 2, size, size);
            base.OnResize(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(Theme.Background);
            Rectangle badge = _badge.Bounds;
            int textX = badge.Right + TextGap;
            TextRenderer.DrawText(graphics, _title, Theme.Title, new Point(textX, badge.Top - 4), Theme.Text);
            TextRenderer.DrawText(graphics, _subtitle, Theme.Small, new Point(textX + 1, badge.Bottom - Theme.Small.Height + 1), Theme.TextMuted);
            using (var rule = new Pen(Theme.Border))
            {
                graphics.DrawLine(rule, 0, Height - 1, Width, Height - 1);
            }
            if (_canMinimize)
            {
                DrawCaptionButton(graphics, CaptionButton.Minimize);
            }
            DrawCaptionButton(graphics, CaptionButton.Close);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            SetHovered(HitTest(e.Location));
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            SetHovered(CaptionButton.None);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && HitTest(e.Location) == CaptionButton.None)
            {
                // Hands the drag to Windows as if the band were the window's own caption.
                User32.ReleaseCapture();
                User32.SendMessage(FindForm().Handle, User32.WmNcLeftButtonDown, new IntPtr(User32.HitTestCaption), IntPtr.Zero);
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            switch (HitTest(e.Location))
            {
                case CaptionButton.Minimize:
                    MinimizeClicked?.Invoke();
                    break;
                case CaptionButton.Close:
                    CloseClicked?.Invoke();
                    break;
            }
            base.OnMouseClick(e);
        }

        private Rectangle ButtonBounds(CaptionButton button)
        {
            int size = (int)(Height * ButtonHeightRatio);
            int slotsFromRight = button == CaptionButton.Close ? 1 : 2;
            return new Rectangle(Width - slotsFromRight * (size + ButtonSpacing) - RightMargin, (Height - size) / 2, size, size);
        }

        private CaptionButton HitTest(Point point)
        {
            if (_canMinimize && ButtonBounds(CaptionButton.Minimize).Contains(point))
            {
                return CaptionButton.Minimize;
            }
            return ButtonBounds(CaptionButton.Close).Contains(point) ? CaptionButton.Close : CaptionButton.None;
        }

        private void SetHovered(CaptionButton button)
        {
            if (_hovered != button)
            {
                _hovered = button;
                Invalidate();
            }
        }

        // Drawn as lines: icon-font glyphs carry their own side bearings and never sit exactly centred.
        private void DrawCaptionButton(Graphics graphics, CaptionButton button)
        {
            Rectangle bounds = ButtonBounds(button);
            if (_hovered == button)
            {
                RoundedRectangle.Fill(graphics, bounds, CaptionButtonRadius, button == CaptionButton.Close ? Theme.Accent : Theme.CaptionHover);
            }
            float centerX = bounds.X + bounds.Width / 2f;
            float centerY = bounds.Y + bounds.Height / 2f;
            float half = bounds.Width * GlyphHalfSizeRatio;
            using (var pen = new Pen(Color.White, Math.Max(1f, bounds.Width / 28f)))
            {
                if (button == CaptionButton.Minimize)
                {
                    graphics.DrawLine(pen, centerX - half, centerY, centerX + half, centerY);
                }
                else
                {
                    graphics.DrawLine(pen, centerX - half, centerY - half, centerX + half, centerY + half);
                    graphics.DrawLine(pen, centerX - half, centerY + half, centerX + half, centerY - half);
                }
            }
        }
    }
}
