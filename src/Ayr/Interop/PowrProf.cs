using System;
using System.Runtime.InteropServices;

namespace Ayr.Interop
{
    internal static class PowrProf
    {
        public const uint Success = 0;

        [DllImport("powrprof.dll")]
        public static extern uint PowerGetActiveScheme(IntPtr rootPowerKey, out IntPtr activeScheme);

        [DllImport("powrprof.dll")]
        public static extern uint PowerSetActiveScheme(IntPtr rootPowerKey, ref Guid scheme);

        [DllImport("powrprof.dll")]
        public static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subgroup, ref Guid setting, out uint value);

        [DllImport("powrprof.dll")]
        public static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subgroup, ref Guid setting, out uint value);

        [DllImport("powrprof.dll")]
        public static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subgroup, ref Guid setting, uint value);

        [DllImport("powrprof.dll")]
        public static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey, ref Guid scheme, ref Guid subgroup, ref Guid setting, uint value);
    }
}
