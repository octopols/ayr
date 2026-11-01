using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    public sealed class LabeledSlider : ThemedControl
    {
        private const float SliderShare = 0.5f;

        private readonly string _caption;
        private readonly Func<int, string> _formatValue;
        private readonly FlatSlider _slider;

        public LabeledSlider(Theme theme, string caption, int minimum, int maximum, Func<int, string> formatValue) : base(theme)
        {
            _caption = caption;
            _formatValue = formatValue;
            BackColor = theme.Surface;
            _slider = new FlatSlider(theme, minimum, maximum) { BackColor = theme.Surface, AccessibleName = caption };
            _slider.ValueChanged += value =>
            {
                Invalidate();
                ValueChanged?.Invoke(value);
            };
            _slider.ValueCommitted += value => ValueCommitted?.Invoke(value);
            Controls.Add(_slider);
        }

        public event Action<int> ValueChanged;

        /// <summary>Raised once the user has finished moving the slider.</summary>
        public event Action<int> ValueCommitted;

        public int Value
        {
            get => _slider.Value;
            set => _slider.Value = value;
        }

        protected override void OnResize(EventArgs e)
        {
            int sliderHeight = (int)(Height * SliderShare);
            _slider.SetBounds(0, Height - sliderHeight, Width, sliderHeight);
            base.OnResize(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _slider.Enabled = Enabled;
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            var line = new Rectangle(0, 0, Width, Height - _slider.Height);
            TextRenderer.DrawText(e.Graphics, _caption, Theme.Body, line, Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, _formatValue(Value), Theme.Body, line, Theme.Text, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }
}
