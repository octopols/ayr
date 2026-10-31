using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>On/off switch. Raises Toggled for user clicks only, never when Checked is set from code.</summary>
    public sealed class ToggleSwitch : ThemedControl
    {
        private const float KnobInset = 3;

        private bool _checked;

        public ToggleSwitch(Theme theme) : base(theme)
        {
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public event Action<bool> Toggled;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            var track = new RectangleF(1, 1, Width - 3, Height - 3);
            RoundedRectangle.Fill(graphics, track, track.Height / 2, _checked ? Theme.Accent : Theme.Track);
            float knob = track.Height - 2 * KnobInset;
            float knobX = _checked ? track.Right - knob - KnobInset : track.X + KnobInset;
            using (var brush = new SolidBrush(_checked ? Theme.AccentText : Theme.TextMuted))
            {
                graphics.FillEllipse(brush, knobX, track.Y + KnobInset, knob, knob);
            }
            DrawFocus(graphics, new RectangleF(0, 0, Width - 1, Height - 1));
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (Enabled)
            {
                Focus();
                Toggled?.Invoke(!_checked);
            }
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsActivationKey(e.KeyCode))
            {
                Toggled?.Invoke(!_checked);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}
