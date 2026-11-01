using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>A one-line notice. With an action label it is clickable, and the label is shown at its right end.</summary>
    public sealed class NoticeBar : ThemedControl
    {
        private const float CornerRadius = 6;
        private const int TextPadding = 14;

        private string _message = "";
        private string _actionLabel;

        public NoticeBar(Theme theme) : base(theme)
        {
        }

        /// <summary>Raised when a notice with an action is clicked.</summary>
        public event Action ActionClicked;

        /// <param name="actionLabel">Null for a notice that only informs.</param>
        public void SetMessage(string message, string actionLabel)
        {
            _message = message;
            _actionLabel = actionLabel;
            Cursor = actionLabel == null ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_actionLabel != null)
            {
                ActionClicked?.Invoke();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            RoundedRectangle.Fill(graphics, bounds, CornerRadius, Theme.NoticeBackground);
            RoundedRectangle.Outline(graphics, bounds, CornerRadius, Theme.NoticeBorder);
            int actionWidth = _actionLabel == null ? 0 : TextRenderer.MeasureText(_actionLabel, Theme.BodyBold).Width + TextPadding;
            var messageBounds = new Rectangle(TextPadding, 0, Width - 2 * TextPadding - actionWidth, Height);
            TextRenderer.DrawText(graphics, _message, Theme.Body, messageBounds, Theme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (_actionLabel != null)
            {
                var actionBounds = new Rectangle(Width - TextPadding - actionWidth, 0, actionWidth, Height);
                TextRenderer.DrawText(graphics, _actionLabel, Theme.BodyBold, actionBounds, Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
        }
    }
}
