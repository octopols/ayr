using System;
using System.Runtime.InteropServices;

namespace Ayr.Interop
{
    internal static class UxTheme
    {
        private const string DarkExplorerTheme = "DarkMode_Explorer";

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string appName, string idList);

        public static void UseDarkScrollbars(IntPtr window) => SetWindowTheme(window, DarkExplorerTheme, null);
    }
}
