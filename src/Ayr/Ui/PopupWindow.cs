using System;
using System.Drawing;
using System.Windows.Forms;
using Ayr.Interop;

namespace Ayr.Ui
{
    /// <summary>
    /// Base for the app's borderless windows: rounded corners, a drop shadow and a one-pixel border, DPI-scaled sizes, and
    /// a theme owned and disposed with the window. Escape closes it.
    /// </summary>
    public abstract class PopupWindow : Form
    {
        private const int DropShadowClassStyle = 0x20000;
        private const int AnchorGap = 12;

        private readonly float _dpiScale;

        protected PopupWindow()
        {
            Theme = new Theme();
            using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero))
            {
                _dpiScale = screen.DpiX / 96f;
            }
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            DoubleBuffered = true;
            KeyPreview = true;
        }

        protected Theme Theme { get; }

        protected virtual int ExtendedWindowStyle => 0;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= DropShadowClassStyle;
                parameters.ExStyle |= ExtendedWindowStyle;
                return parameters;
            }
        }

        public void PlaceNear(TrayAnchor anchor) => Location = anchor.Place(Size, Scaled(AnchorGap));

        protected int Scaled(int pixels) => (int)Math.Round(pixels * _dpiScale);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dwmapi.RoundCorners(Handle);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Border))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Theme.Dispose();
            }
        }
    }
}
