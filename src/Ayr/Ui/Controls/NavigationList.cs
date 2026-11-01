using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>The sidebar's page list: one row per page, the current one highlighted with an accent bar.</summary>
    public sealed class NavigationList : ThemedControl
    {
        private const int AccentBarWidth = 3;
        private const float RowRadius = 6;
        private const int TextInset = 14;

        private readonly IReadOnlyList<string> _pages;
        private readonly int _rowHeight;
        private int _selected;
        private int _hovered = -1;

        public NavigationList(Theme theme, IReadOnlyList<string> pages, int rowHeight) : base(theme)
        {
            _pages = pages;
            _rowHeight = rowHeight;
            TakesFocus();
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PageTabList;
            AccessibleName = "Pages";
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new OwnerDrawnAccessibility(this, value: () => _pages[_selected]);

        /// <summary>Raised when the user picks a page.</summary>
        public event Action<int> Picked;

        public int PreferredHeight => _pages.Count * _rowHeight;

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
            for (int page = 0; page < _pages.Count; page++)
            {
                var row = new RectangleF(0, page * _rowHeight + 2, Width - 1, _rowHeight - 4);
                bool selected = page == _selected;
                if (selected || page == _hovered)
                {
                    RoundedRectangle.Fill(graphics, row, RowRadius, selected ? Theme.SegmentSelected : Theme.SurfaceAlt);
                }
                if (selected)
                {
                    float barHeight = row.Height / 2;
                    RoundedRectangle.Fill(graphics, new RectangleF(row.X + 2, row.Y + (row.Height - barHeight) / 2, AccentBarWidth, barHeight), AccentBarWidth / 2f, Theme.Accent);
                }
                var text = new Rectangle(TextInset, (int)row.Y, Width - TextInset, (int)row.Height);
                TextRenderer.DrawText(graphics, _pages[page], selected ? Theme.BodyBold : Theme.Body, text, selected ? Theme.Text : Theme.TextMuted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if (Focused && ShowFocusCues)
            {
                DrawFocus(graphics, new RectangleF(0, _selected * _rowHeight, Width - 1, _rowHeight - 1));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hovered = RowAt(e.Y);
            if (hovered != _hovered)
            {
                _hovered = hovered;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int page = RowAt(e.Y);
            if (page >= 0)
            {
                Pick(page);
            }
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                int step = e.KeyCode == Keys.Up ? -1 : 1;
                Pick((_selected + step + _pages.Count) % _pages.Count);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private int RowAt(int y)
        {
            int page = y / _rowHeight;
            return page >= 0 && page < _pages.Count ? page : -1;
        }

        private void Pick(int page)
        {
            SelectedIndex = page;
            Picked?.Invoke(page);
        }
    }
}
