using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>One row of choices. Raises Picked for user choices only, never when SelectedIndex is set from code.</summary>
    public sealed class Segmented : ThemedControl
    {
        private const int None = -1;
        private const float TrackRadius = 7;
        private const float SelectionRadius = 6;
        private const float SelectionInset = 3;

        private readonly IReadOnlyList<string> _items;
        private int _selected = None;
        private int _hovered = None;

        public Segmented(Theme theme, IReadOnlyList<string> items) : base(theme)
        {
            _items = items;
            TakesFocus();
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.List;
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibility(this, value: () => _selected == None ? "" : _items[_selected]);

        public event Action<int> Picked;

        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            var track = new RectangleF(0, 0, Width - 1, Height - 1);
            RoundedRectangle.Fill(graphics, track, TrackRadius, Theme.SurfaceAlt);
            for (int index = 0; index < _items.Count; index++)
            {
                DrawItem(graphics, index);
            }
            DrawFocus(graphics, track);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            SetHovered(HitTest(e.Location));
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            SetHovered(None);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int hit = HitTest(e.Location);
            if (hit != None && Enabled)
            {
                Focus();
                Picked?.Invoke(hit);
            }
            base.OnMouseClick(e);
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int current = _selected == None ? 0 : _selected;
            int? next = null;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down)
            {
                next = (current + 1) % _items.Count;
            }
            else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
            {
                next = (current + _items.Count - 1) % _items.Count;
            }
            else if (IsActivationKey(e.KeyCode))
            {
                next = current;
            }
            if (next.HasValue)
            {
                Picked?.Invoke(next.Value);
                e.Handled = true;
            }
            base.OnKeyDown(e);
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

        private void DrawItem(Graphics graphics, int index)
        {
            RectangleF cell = Cell(index);
            bool selected = index == _selected;
            if (selected)
            {
                RectangleF selection = RectangleF.Inflate(cell, -SelectionInset, -SelectionInset);
                RoundedRectangle.Fill(graphics, selection, SelectionRadius, Theme.SegmentSelected);
                RoundedRectangle.Outline(graphics, selection, SelectionRadius, Theme.Border);
            }
            bool emphasized = Enabled && (selected || index == _hovered);
            TextRenderer.DrawText(graphics, _items[index], selected ? Theme.BodyBold : Theme.Body, Rectangle.Round(cell),
                emphasized ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private RectangleF Cell(int index)
        {
            float width = (Width - 1) / (float)_items.Count;
            return new RectangleF(index * width, 0, width, Height - 1);
        }

        private int HitTest(Point point)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                if (Cell(index).Contains(point))
                {
                    return index;
                }
            }
            return None;
        }

        private void SetHovered(int index)
        {
            if (_hovered != index)
            {
                _hovered = index;
                Invalidate();
            }
        }
    }
}
