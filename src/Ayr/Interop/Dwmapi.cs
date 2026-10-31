using System;
using System.Runtime.InteropServices;

namespace Ayr.Interop
{
    internal static class Dwmapi
    {
        public const int ExtendedFrameBounds = 9;
        public const int WindowCornerPreference = 33;
        public const int CornerRound = 2;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);

        public static void RoundCorners(IntPtr window)
        {
            int preference = CornerRound;
            DwmSetWindowAttribute(window, WindowCornerPreference, ref preference, sizeof(int));
        }
    }
}
