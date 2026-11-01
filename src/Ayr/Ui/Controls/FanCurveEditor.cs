using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Ayr.Ui.Controls
{
    /// <summary>
    /// A fan curve as dots on a chart, one per temperature step, dragged up and down. Auto's curve is drawn faintly
    /// behind it for reference. Each step only moves as far as the caller's limits allow.
    /// </summary>
    public sealed class FanCurveEditor : ThemedControl
    {
        private const int MaxDuty = 100;
        private const float DotRadius = 6;
        private const int GrabDistance = 18;
        private const int LabelGap = 4;

        private IReadOnlyList<byte> _curve = new byte[0];
        private IReadOnlyList<byte> _reference = new byte[0];
        private IReadOnlyList<byte> _minimums = new byte[0];
        private IReadOnlyList<string> _labels = new string[0];
        private int _dragged = -1;
        private int _hovered = -1;

        public FanCurveEditor(Theme theme) : base(theme)
        {
            BackColor = theme.Surface;
            Cursor = Cursors.Default;
        }

        /// <summary>Raised while dragging, with the step and its new duty.</summary>
        public event Action<int, byte> PointDragged;

        /// <summary>Raised once the user lets go of a step.</summary>
        public event Action DragFinished;

        public void SetCurve(IReadOnlyList<byte> curve, IReadOnlyList<byte> reference, IReadOnlyList<byte> minimums, IReadOnlyList<string> labels)
        {
            _curve = curve;
            _reference = reference;
            _minimums = minimums;
            _labels = labels;
            Invalidate();
        }

        public bool IsDragging => _dragged >= 0;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Smooth(graphics);
            graphics.Clear(BackColor);
            if (_curve.Count == 0)
            {
                return;
            }
            Rectangle plot = PlotArea();
            DrawGrid(graphics, plot);
            DrawLine(graphics, plot, _reference, Theme.Border, dashed: true);
            DrawLine(graphics, plot, _curve, Theme.Accent, dashed: false);
            for (int point = 0; point < _curve.Count; point++)
            {
                DrawStep(graphics, plot, point);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _dragged = NearestPoint(e.Location);
                DragTo(e.Y);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (IsDragging)
            {
                DragTo(e.Y);
                return;
            }
            int hovered = NearestPoint(e.Location);
            Cursor = hovered >= 0 ? Cursors.SizeNS : Cursors.Default;
            if (hovered != _hovered)
            {
                _hovered = hovered;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (IsDragging)
            {
                _dragged = -1;
                Invalidate();
                DragFinished?.Invoke();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = -1;
            Invalidate();
        }

        private Rectangle PlotArea()
        {
            int labelHeight = Theme.Small.Height + LabelGap;
            int axisWidth = TextRenderer.MeasureText("100%", Theme.Small).Width + LabelGap;
            int inset = (int)DotRadius + 2;
            return new Rectangle(axisWidth + inset, labelHeight + inset, Width - axisWidth - 2 * inset, Height - 2 * labelHeight - 2 * inset);
        }

        private PointF Position(Rectangle plot, int point, byte duty)
        {
            float step = _curve.Count > 1 ? plot.Width / (float)(_curve.Count - 1) : 0;
            return new PointF(plot.Left + point * step, plot.Bottom - plot.Height * duty / (float)MaxDuty);
        }

        private void DrawGrid(Graphics graphics, Rectangle plot)
        {
            using (var pen = new Pen(Theme.SurfaceAlt))
            {
                foreach (int duty in new[] { 0, 50, MaxDuty })
                {
                    float y = plot.Bottom - plot.Height * duty / (float)MaxDuty;
                    graphics.DrawLine(pen, plot.Left, y, plot.Right, y);
                    var label = new Rectangle(0, (int)y - Theme.Small.Height / 2, plot.Left - LabelGap - (int)DotRadius, Theme.Small.Height);
                    TextRenderer.DrawText(graphics, duty + "%", Theme.Small, label, Theme.TextMuted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private void DrawLine(Graphics graphics, Rectangle plot, IReadOnlyList<byte> curve, Color color, bool dashed)
        {
            if (curve.Count < 2)
            {
                return;
            }
            using (var pen = new Pen(color, 2) { DashStyle = dashed ? DashStyle.Dash : DashStyle.Solid })
            {
                graphics.DrawLines(pen, curve.Select((duty, point) => Position(plot, point, duty)).ToArray());
            }
        }

        // The dot, its temperature below the chart, and its duty above it while hovered or dragged.
        private void DrawStep(Graphics graphics, Rectangle plot, int point)
        {
            PointF center = Position(plot, point, _curve[point]);
            bool active = point == _dragged || (_dragged < 0 && point == _hovered);
            float radius = active ? DotRadius + 1 : DotRadius;
            using (var fill = new SolidBrush(active ? Theme.Text : Theme.Accent))
            {
                graphics.FillEllipse(fill, center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
            }
            int labelWidth = TextRenderer.MeasureText("100%", Theme.Small).Width + LabelGap;
            var temperature = new Rectangle((int)center.X - labelWidth / 2, plot.Bottom + (int)DotRadius + LabelGap, labelWidth, Theme.Small.Height);
            TextRenderer.DrawText(graphics, point < _labels.Count ? _labels[point] : "", Theme.Small, temperature, Theme.TextMuted, TextFormatFlags.HorizontalCenter);
            if (active)
            {
                var duty = new Rectangle((int)center.X - labelWidth / 2, (int)(center.Y - radius) - Theme.Small.Height - LabelGap, labelWidth, Theme.Small.Height);
                TextRenderer.DrawText(graphics, _curve[point] + "%", Theme.Small, duty, Theme.Text, TextFormatFlags.HorizontalCenter);
            }
        }

        private int NearestPoint(Point location)
        {
            if (_curve.Count == 0)
            {
                return -1;
            }
            Rectangle plot = PlotArea();
            int nearest = Enumerable.Range(0, _curve.Count)
                .OrderBy(point => Math.Abs(Position(plot, point, _curve[point]).X - location.X))
                .First();
            return Math.Abs(Position(plot, nearest, _curve[nearest]).X - location.X) <= GrabDistance ? nearest : -1;
        }

        private void DragTo(int y)
        {
            if (!IsDragging)
            {
                return;
            }
            Rectangle plot = PlotArea();
            double fraction = (plot.Bottom - y) / (double)plot.Height;
            int duty = (int)Math.Round(fraction * MaxDuty);
            int minimum = _dragged < _minimums.Count ? _minimums[_dragged] : 0;
            byte clamped = (byte)Math.Max(minimum, Math.Min(MaxDuty, duty));
            if (clamped != _curve[_dragged])
            {
                PointDragged?.Invoke(_dragged, clamped);
            }
        }
    }
}
