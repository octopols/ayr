using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class FlatSlider : ThemedControl
    {
        private const float TrackInset = 8;
        private const float TrackThickness = 4;
        private const float ThumbRadius = 7;
        private const int PageStep = 10;
        private static readonly Keys[] SteppingKeys = { Keys.Left, Keys.Right, Keys.PageDown, Keys.PageUp, Keys.Home, Keys.End };

        private readonly int _minimum;
        private readonly int _maximum;
        private int _value;
        private bool _dragging;

        public FlatSlider(Theme theme, int minimum, int maximum) : base(theme)
        {
            _minimum = minimum;
            _maximum = maximum;
            _value = minimum;
            TakesFocus();
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
        }

        public event Action<int> ValueChanged;

        /// <summary>Raised when the user lets go of the thumb or releases a key: the moment to act on a value.</summary>
        public event Action<int> ValueCommitted;

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibility(this, value: () => _value.ToString());

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (clamped != _value)
                {
                    _value = clamped;
                    Invalidate();
                    ValueChanged?.Invoke(_value);
                }
            }
        }

        private float TrackLeft => TrackInset;

        private float TrackRight => Width - TrackInset;

        private float Fraction => _maximum == _minimum ? 0 : (_value - _minimum) / (float)(_maximum - _minimum);

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            float y = Height / 2f;
            float thumbX = TrackLeft + (TrackRight - TrackLeft) * Fraction;
            using (var track = new Pen(Theme.Track, TrackThickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var filled = new Pen(Theme.Accent, TrackThickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                graphics.DrawLine(track, TrackLeft, y, TrackRight, y);
                if (thumbX > TrackLeft)
                {
                    graphics.DrawLine(filled, TrackLeft, y, thumbX, y);
                }
            }
            var thumb = new RectangleF(thumbX - ThumbRadius, y - ThumbRadius, 2 * ThumbRadius, 2 * ThumbRadius);
            using (var fill = new SolidBrush(Theme.Surface))
            using (var ring = new Pen(Theme.Accent, 2))
            {
                graphics.FillEllipse(fill, thumb);
                graphics.DrawEllipse(ring, thumb);
            }
            DrawFocus(graphics, new RectangleF(0, 0, Width - 1, Height - 1));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Enabled)
            {
                Focus();
                _dragging = true;
                SetFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                SetFromX(e.X);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                ValueCommitted?.Invoke(_value);
            }
            base.OnMouseUp(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (Array.IndexOf(SteppingKeys, e.KeyCode) >= 0)
            {
                ValueCommitted?.Invoke(_value);
            }
            base.OnKeyUp(e);
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: Value -= 1; break;
                case Keys.Right: Value += 1; break;
                case Keys.PageDown: Value -= PageStep; break;
                case Keys.PageUp: Value += PageStep; break;
                case Keys.Home: Value = _minimum; break;
                case Keys.End: Value = _maximum; break;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            e.Handled = true;
        }

        private void SetFromX(int x)
        {
            float fraction = Math.Max(0, Math.Min(1, (x - TrackLeft) / (TrackRight - TrackLeft)));
            Value = _minimum + (int)Math.Round(fraction * (_maximum - _minimum));
        }
    }
}
