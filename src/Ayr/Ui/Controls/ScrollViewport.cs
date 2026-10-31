using System;
using System.Drawing;
using System.Windows.Forms;
using Ayr.Interop;

namespace Ayr.Ui.Controls
{
    /// <summary>
    /// A scrolling panel that never jumps on its own. WinForms scrolls a container to whichever child takes focus, which
    /// sent the page back to the top on every click; here only the user moves the scroll position.
    /// </summary>
    public sealed class ScrollViewport : Panel
    {
        private const int PixelsPerLine = 16;
        private const int WheelDelta = 120;

        public ScrollViewport()
        {
            AutoScroll = true;
        }

        public void ScrollByWheel(int wheelDelta)
        {
            int step = SystemInformation.MouseWheelScrollLines * PixelsPerLine * wheelDelta / WheelDelta;
            int top = -AutoScrollPosition.Y;
            AutoScrollPosition = new Point(0, Math.Max(0, top - step));
        }

        protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UxTheme.UseDarkScrollbars(Handle);
        }
    }
}
