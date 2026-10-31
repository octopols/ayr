using System;
using Ayr.Interop;

namespace Ayr.App
{
    public static class WorkingSet
    {
        private static readonly IntPtr TrimToMinimum = new IntPtr(-1);

        /// <summary>Hands idle memory back to Windows; pages come back on demand if touched again.</summary>
        public static void Trim()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Kernel32.SetProcessWorkingSetSize(Kernel32.GetCurrentProcess(), TrimToMinimum, TrimToMinimum);
        }
    }
}
