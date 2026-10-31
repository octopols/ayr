using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Ayr.Interop;

namespace Ayr.Ui
{
    /// <summary>
    /// Where a popup goes for a tray icon click, the way tray flyouts such as EarTrumpet's do it: find the taskbar edge,
    /// then sit just outside whatever shell surface holds the icon. That is the taskbar itself, or the "hidden icons"
    /// overflow flyout, which the popup must clear too. Captured on mouse-down, while that flyout is still open.
    /// </summary>
    public sealed class TrayAnchor
    {
        private const int ClassNameCapacity = 64;
        private static readonly string[] TaskbarClasses = { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

        private readonly Point _click;
        private readonly Screen _screen;
        private readonly TaskbarEdge _edge;
        private readonly Rectangle _shellSurface;

        private TrayAnchor(Point click)
        {
            _click = click;
            _screen = Screen.FromPoint(click);
            _edge = FindTaskbar(_screen, out Rectangle taskbar);
            _shellSurface = TryFindOverflowFlyout(out Rectangle flyout) ? Rectangle.Union(taskbar, flyout) : taskbar;
        }

        private enum TaskbarEdge
        {
            Left = 0,
            Top = 1,
            Right = 2,
            Bottom = 3
        }

        public static TrayAnchor Capture(Point click) => new TrayAnchor(click);

        public Point Place(Size window, int gap)
        {
            Rectangle work = _screen.WorkingArea;
            Point place;
            switch (_edge)
            {
                case TaskbarEdge.Top:
                    place = new Point(_click.X - window.Width / 2, Math.Max(work.Top, _shellSurface.Bottom) + gap);
                    break;
                case TaskbarEdge.Left:
                    place = new Point(Math.Max(work.Left, _shellSurface.Right) + gap, _click.Y - window.Height / 2);
                    break;
                case TaskbarEdge.Right:
                    place = new Point(Math.Min(work.Right, _shellSurface.Left) - window.Width - gap, _click.Y - window.Height / 2);
                    break;
                default:
                    place = new Point(_click.X - window.Width / 2, Math.Min(work.Bottom, _shellSurface.Top) - window.Height - gap);
                    break;
            }
            int x = Math.Max(work.Left + gap, Math.Min(place.X, work.Right - window.Width - gap));
            int y = Math.Max(work.Top + gap, Math.Min(place.Y, work.Bottom - window.Height - gap));
            return new Point(x, y);
        }

        // The taskbar sits where the working area is smaller than the screen; an auto-hiding one is asked from the shell.
        private static TaskbarEdge FindTaskbar(Screen screen, out Rectangle taskbar)
        {
            Rectangle bounds = screen.Bounds;
            Rectangle work = screen.WorkingArea;
            if (work.Bottom < bounds.Bottom)
            {
                taskbar = Rectangle.FromLTRB(bounds.Left, work.Bottom, bounds.Right, bounds.Bottom);
                return TaskbarEdge.Bottom;
            }
            if (work.Top > bounds.Top)
            {
                taskbar = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, work.Top);
                return TaskbarEdge.Top;
            }
            if (work.Left > bounds.Left)
            {
                taskbar = Rectangle.FromLTRB(bounds.Left, bounds.Top, work.Left, bounds.Bottom);
                return TaskbarEdge.Left;
            }
            if (work.Right < bounds.Right)
            {
                taskbar = Rectangle.FromLTRB(work.Right, bounds.Top, bounds.Right, bounds.Bottom);
                return TaskbarEdge.Right;
            }
            var data = new Shell32.AppBarData { Size = Marshal.SizeOf(typeof(Shell32.AppBarData)) };
            if (Shell32.SHAppBarMessage(Shell32.AppBarGetTaskbarPosition, ref data) != IntPtr.Zero)
            {
                taskbar = data.Rect.ToRectangle();
                return (TaskbarEdge)data.Edge;
            }
            taskbar = Rectangle.FromLTRB(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
            return TaskbarEdge.Bottom;
        }

        // The click is on a tray icon, so the window under it is a taskbar or the overflow flyout
        // ("TopLevelWindowForOverflowXamlIsland" on Windows 11, "NotifyIconOverflowWindow" on Windows 10).
        private bool TryFindOverflowFlyout(out Rectangle flyout)
        {
            flyout = Rectangle.Empty;
            IntPtr window = User32.GetAncestor(User32.WindowFromPoint(new NativePoint(_click)), User32.GetAncestorRoot);
            if (window == IntPtr.Zero || IsTaskbar(window) || !TryGetBounds(window, out Rectangle bounds))
            {
                return false;
            }
            Rectangle screen = _screen.Bounds;
            bool popupSized = bounds.Width <= screen.Width / 2 && bounds.Height <= screen.Height / 2;
            if (!bounds.Contains(_click) || !popupSized)
            {
                return false;
            }
            flyout = bounds;
            return true;
        }

        private static bool IsTaskbar(IntPtr window)
        {
            var className = new StringBuilder(ClassNameCapacity);
            User32.GetClassName(window, className, className.Capacity);
            return Array.IndexOf(TaskbarClasses, className.ToString()) >= 0;
        }

        private static bool TryGetBounds(IntPtr window, out Rectangle bounds)
        {
            bool found = Dwmapi.DwmGetWindowAttribute(window, Dwmapi.ExtendedFrameBounds, out NativeRect rect, Marshal.SizeOf(typeof(NativeRect))) == 0
                || User32.GetWindowRect(window, out rect);
            bounds = found ? rect.ToRectangle() : Rectangle.Empty;
            return found;
        }
    }
}
