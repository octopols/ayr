using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>Short facts, one per line: a muted label on the left and its value beside it.</summary>
    public sealed class FactList : ThemedControl
    {
        private const int LabelGap = 24;

        private IReadOnlyList<Fact> _facts = Array.Empty<Fact>();

        public FactList(Theme theme) : base(theme)
        {
            AccessibleRole = AccessibleRole.StaticText;
        }

        public void ShowFacts(IReadOnlyList<Fact> facts)
        {
            _facts = facts;
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            Invalidate();
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibility(this, value: () => SpokenFacts);

        private string SpokenFacts => string.Join(". ", _facts.Select(fact => fact.Label + ": " + fact.Value));

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(BackColor);
            if (_facts.Count == 0)
            {
                return;
            }
            int lineHeight = Height / _facts.Count;
            int labelWidth = _facts.Max(fact => TextRenderer.MeasureText(fact.Label, Theme.Small).Width) + LabelGap;
            for (int line = 0; line < _facts.Count; line++)
            {
                var label = new Rectangle(0, line * lineHeight, labelWidth, lineHeight);
                var value = new Rectangle(labelWidth, line * lineHeight, Width - labelWidth, lineHeight);
                TextRenderer.DrawText(graphics, _facts[line].Label, Theme.Small, label, Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(graphics, _facts[line].Value, Theme.Small, value, Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }
}
