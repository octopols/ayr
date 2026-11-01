using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Ui.Controls;

namespace Ayr.Ui
{
    /// <summary>
    /// The custom fan curve, opened from the Custom fan mode. Each change is applied as soon as a point is let go, so the
    /// fans can be heard reacting while the window is still open. Changes go through the backend's queue rather than the
    /// main window's, which drops a change while another is running, so a quick second drag is never lost.
    /// </summary>
    public sealed class FanCurveWindow : PopupWindow
    {
        private const int BaseWidth = 620;
        private const int BaseHeight = 470;
        private const int HeaderHeight = 60;
        private const int EdgePadding = 20;
        private const int CardPadding = 14;
        private const string Hint = "Fans never go below a safe minimum, so some points stop early.";
        private const string ResetLabel = "Reset to MSI's curve";
        private const string UndoResetLabel = "Undo reset";

        private readonly UiBackend _backend;
        private readonly CurveTemperatures _temperatures;
        private readonly HeaderBand _header;
        private readonly Card _card;
        private readonly Segmented _fanPicker;
        private readonly CurveLegend _legend;
        private readonly FanCurveEditor _editor;
        private readonly MutedText _status;
        private readonly FlatButton _reset;
        private readonly FlatButton _done;

        private FanCurves _curves;
        private FanCurves _curvesBeforeReset;
        private FanKind _fan = FanKind.Cpu;

        public FanCurveWindow(UiBackend backend, FanCurves curves, CurveTemperatures temperatures)
        {
            _backend = backend;
            _curves = curves;
            _temperatures = temperatures;
            Text = "Custom fan curve";

            _header = new HeaderBand(Theme, Text, "Drag a point up or down. It applies when you let go.", canMinimize: false);
            _header.CloseClicked += Close;
            _card = new Card(Theme);
            _fanPicker = new Segmented(Theme, new[] { "CPU fan", "GPU fan" }) { SelectedIndex = 0, AccessibleName = "Fan" };
            _fanPicker.Picked += index =>
            {
                _fan = index == 0 ? FanKind.Cpu : FanKind.Gpu;
                _fanPicker.SelectedIndex = index;
                ShowCurve();
            };
            _legend = new CurveLegend(Theme);
            _editor = new FanCurveEditor(Theme);
            _editor.PointDragged += (point, duty) =>
            {
                _curves = _curves.With(_fan, point, duty);
                ShowCurve();
            };
            _editor.DragFinished += () =>
            {
                ForgetUndo();
                Apply(_curves);
            };
            _card.Controls.AddRange(new Control[] { _fanPicker, _legend, _editor });

            _status = new MutedText(Theme, Hint, Theme.Background);
            _reset = new FlatButton(Theme, ResetLabel, ButtonStyle.Secondary);
            _reset.Click += (sender, e) => ResetOrUndo();
            _done = new FlatButton(Theme, "Done", ButtonStyle.Primary);
            _done.Click += (sender, e) => Close();

            Controls.AddRange(new Control[] { _header, _card, _status, _reset, _done });
            ShowCurve();
            LayoutContent();
        }

        // A reset throws away the user's shape, so the button offers it back until they shape the curve again.
        private void ResetOrUndo()
        {
            if (_curvesBeforeReset == null)
            {
                _curvesBeforeReset = _curves;
                _curves = FanCurves.Stock;
                _reset.Text = UndoResetLabel;
            }
            else
            {
                _curves = _curvesBeforeReset;
                ForgetUndo();
            }
            ShowCurve();
            Apply(_curves);
        }

        private void ForgetUndo()
        {
            _curvesBeforeReset = null;
            _reset.Text = ResetLabel;
        }

        private void Apply(FanCurves curves)
        {
            _backend.Run(this, () => _backend.ApplyCustomCurves(curves), (message, error) =>
            {
                if (error != null)
                {
                    ActivityLog.Write("applying the fan curve failed: " + ExceptionText.Of(error));
                }
                _status.Text = error == null ? "Saved. Fans now follow your curve." : "Couldn't apply the curve. Your previous curve is still in use.";
            });
        }

        private void ShowCurve()
        {
            IReadOnlyList<byte> stock = FanCurves.Stock.For(_fan);
            IReadOnlyList<byte> minimums = Enumerable.Range(0, stock.Count).Select(point => FanCurves.MinimumDuty(_fan, point)).ToList();
            _editor.SetCurve(_curves.For(_fan), stock, minimums, _temperatures.For(_fan));
        }

        private void LayoutContent()
        {
            int width = Scaled(BaseWidth);
            int height = Scaled(BaseHeight);
            ClientSize = new Size(width + 2, height + 2);
            int padding = Scaled(EdgePadding);
            int headerHeight = Scaled(HeaderHeight);
            _header.SetBounds(1, 1, width, headerHeight);

            int buttonHeight = Scaled(34);
            int footerTop = 1 + height - padding - buttonHeight;
            int doneWidth = Scaled(96);
            int resetWidth = Scaled(130);
            int right = 1 + width - padding;
            _done.SetBounds(right - doneWidth, footerTop, doneWidth, buttonHeight);
            _reset.SetBounds(_done.Left - Scaled(8) - resetWidth, footerTop, resetWidth, buttonHeight);
            _status.SetBounds(1 + padding, footerTop, _reset.Left - Scaled(12) - 1 - padding, buttonHeight);

            int cardTop = 1 + headerHeight + padding;
            _card.SetBounds(1 + padding, cardTop, width - 2 * padding, footerTop - Scaled(16) - cardTop);
            int inner = Scaled(CardPadding);
            int pickerWidth = Scaled(200);
            int pickerHeight = Scaled(30);
            int legendLeft = inner + pickerWidth + Scaled(12);
            _fanPicker.SetBounds(inner, inner, pickerWidth, pickerHeight);
            _legend.SetBounds(legendLeft, inner, _card.Width - inner - legendLeft, pickerHeight);
            int editorTop = inner + pickerHeight + Scaled(12);
            _editor.SetBounds(inner, editorTop, _card.Width - 2 * inner, _card.Height - editorTop - inner);
        }
    }
}
