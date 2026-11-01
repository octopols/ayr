using System;
using Ayr.Interop;

namespace Ayr.Battery
{
    public static class SystemClock
    {
        public static DateTime BootTime => DateTime.Now - TimeSpan.FromMilliseconds(Kernel32.GetTickCount64());
    }
}
