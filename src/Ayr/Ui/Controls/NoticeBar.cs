using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>
    /// A notice of up to two lines. With an action label it is a button: clicked, or Enter or Space while focused, and the
    /// label is shown at its right end.
    /// </summary>
    public sealed class NoticeBar : ThemedControl
    {
        private const float CornerRadius = 6;
        private const int TextPadding = 14;
        private const TextFormatFlags MessageLayout = TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

        private string _message = "";
        private string _actionLabel;

        public NoticeBar(Theme theme) : base(theme)
        {
            TakesFocus();
            AccessibleRole = AccessibleRole.Alert;
        }

        /// <summary>Raised when a notice with an action is clicked.</summary>
        public event Action ActionClicked;

        /// <param name="actionLabel">Null for a notice that only informs.</param>
        public void SetMessage(string message, string actionLabel)
        {
            _message = message;
            _actionLabel = actionLabel;
            Cursor = actionLabel == null ? Cursors.Default : Cursors.Hand;
            TabStop = actionLabel != null;
            AccessibleRole = actionLabel == null ? AccessibleRole.Alert : AccessibleRole.PushButton;
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
            Invalidate();
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibility(this, name: () => _actionLabel == null ? _message : _message + " " + _actionLabel);

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            RunAction();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (IsActivationKey(e.KeyCode))
            {
                RunAction();
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        private void RunAction()
        {
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
            int messageWidth = Width - 2 * TextPadding - actionWidth;
            // Wrapped text can't be centred vertically by the renderer, so it is measured and placed.
            int messageHeight = Math.Min(Height, TextRenderer.MeasureText(_message, Theme.Small, new Size(messageWidth, Height), MessageLayout).Height);
            var messageBounds = new Rectangle(TextPadding, (Height - messageHeight) / 2, messageWidth, messageHeight);
            TextRenderer.DrawText(graphics, _message, Theme.Small, messageBounds, Theme.Accent, MessageLayout);
            if (_actionLabel != null)
            {
                var actionBounds = new Rectangle(Width - TextPadding - actionWidth, 0, actionWidth, Height);
                TextRenderer.DrawText(graphics, _actionLabel, Theme.BodyBold, actionBounds, Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
            DrawFocus(graphics, bounds);
        }
    }
}
