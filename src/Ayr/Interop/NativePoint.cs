using System.Drawing;
using System.Runtime.InteropServices;

namespace Ayr.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(Point point)
        {
            X = point.X;
            Y = point.Y;
        }
    }
}
