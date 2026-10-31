using System;
using System.Runtime.InteropServices;

namespace Ayr.Interop
{
    internal static class Shell32
    {
        public const uint AppBarGetTaskbarPosition = 5;

        [StructLayout(LayoutKind.Sequential)]
        public struct AppBarData
        {
            public int Size;
            public IntPtr Window;
            public uint CallbackMessage;
            public uint Edge;
            public NativeRect Rect;
            public IntPtr Param;
        }

        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint message, ref AppBarData data);
    }
}
