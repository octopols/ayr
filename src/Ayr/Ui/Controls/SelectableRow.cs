using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>A row in a card list with a name, a detail line and a switch on the right that selects it.</summary>
    public sealed class SelectableRow : ThemedControl
    {
        private const int SwitchWidth = 40;
        private const int SwitchHeight = 22;
        private const int Gap = 12;

        private readonly ToggleSwitch _switch;
        private string _name = "";
        private string _detail = "";

        public SelectableRow(Theme theme) : base(theme)
        {
            _switch = new ToggleSwitch(theme);
            _switch.Toggled += selected =>
            {
                _switch.Checked = selected;
                SelectionChanged?.Invoke();
            };
            Controls.Add(_switch);
        }

        /// <summary>Raised when the user flips the switch.</summary>
        public event Action SelectionChanged;

        public bool Selected
        {
            get => _switch.Checked;
            set => _switch.Checked = value;
        }

        public void SetText(string name, string detail)
        {
            _name = name;
            _detail = detail;
            _switch.AccessibleName = name;
            _switch.AccessibleDescription = detail;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _switch.SetBounds(Width - SwitchWidth, (Height - SwitchHeight) / 2, SwitchWidth, SwitchHeight);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _switch.Enabled = Enabled;
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(BackColor);
            int textWidth = _switch.Left - Gap;
            var nameBounds = new Rectangle(0, 2, textWidth, Theme.BodyBold.Height + 2);
            var detailBounds = new Rectangle(0, Theme.BodyBold.Height + 4, textWidth, Theme.Small.Height + 2);
            TextRenderer.DrawText(graphics, _name, Theme.BodyBold, nameBounds, Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(graphics, _detail, Theme.Small, detailBounds, Theme.TextMuted, TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
