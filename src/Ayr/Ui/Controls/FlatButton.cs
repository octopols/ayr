using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class FlatButton : ThemedControl
    {
        private const float CornerRadius = 6;
        private const float PrimaryHoverLighten = 0.1f;

        private readonly ButtonStyle _style;
        private bool _hovered;
        private bool _inactive;
        private bool _activationKeyHeld;

        public FlatButton(Theme theme, string text, ButtonStyle style) : base(theme)
        {
            Text = text;
            _style = style;
            TakesFocus();
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
        }

        /// <summary>Looks disabled and ignores clicks, but unlike a disabled control it still gets hover, so a tooltip can say why.</summary>
        public bool Inactive
        {
            get => _inactive;
            set
            {
                if (_inactive != value)
                {
                    _inactive = value;
                    UpdateCursor();
                    Invalidate();
                    AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                }
            }
        }

        private bool Dimmed => !Enabled || _inactive;

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibility(this, name: () => Text, states: () => _inactive ? AccessibleStates.Unavailable : AccessibleStates.None);

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            RoundedRectangle.Fill(graphics, bounds, CornerRadius, FillColor());
            if (_style == ButtonStyle.Secondary)
            {
                RoundedRectangle.Outline(graphics, bounds, CornerRadius, Theme.Border);
            }
            Color text = Dimmed ? Theme.TextMuted : _style == ButtonStyle.Primary ? Theme.AccentText : Theme.Text;
            TextRenderer.DrawText(graphics, Text, Theme.BodyBold, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            DrawFocus(graphics, bounds);
        }

        protected override void OnClick(EventArgs e)
        {
            if (!_inactive)
            {
                base.OnClick(e);
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
            base.OnTextChanged(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            SetHovered(true);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            SetHovered(false);
            base.OnMouseLeave(e);
        }

        // Like a Windows button it acts when the key is released, so holding the key can't press it again, which would
        // walk straight through a two-step confirmation.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsActivationKey(e.KeyCode))
            {
                _activationKeyHeld = true;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (IsActivationKey(e.KeyCode) && _activationKeyHeld)
            {
                _activationKeyHeld = false;
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            _activationKeyHeld = false;
            base.OnLostFocus(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            UpdateCursor();
            Invalidate();
            base.OnEnabledChanged(e);
        }

        private void UpdateCursor() => Cursor = Dimmed ? Cursors.Default : Cursors.Hand;

        private Color FillColor()
        {
            if (Dimmed)
            {
                return Theme.SurfaceAlt;
            }
            if (_style == ButtonStyle.Primary)
            {
                return _hovered ? ControlPaint.Light(Theme.Accent, PrimaryHoverLighten) : Theme.Accent;
            }
            return _hovered ? Theme.SurfaceAlt : Theme.Surface;
        }

        private void SetHovered(bool hovered)
        {
            _hovered = hovered;
            Invalidate();
        }
    }
}
