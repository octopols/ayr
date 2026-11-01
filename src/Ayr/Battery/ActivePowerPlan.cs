using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Ayr.Interop;

namespace Ayr.Battery
{
    /// <summary>Reads and changes settings of the power plan Windows is using now, whichever plan that is.</summary>
    public sealed class ActivePowerPlan
    {
        public PowerValues Read(PowerSetting setting)
        {
            Guid scheme = ActiveScheme();
            Guid subgroup = setting.Subgroup;
            Guid id = setting.Setting;
            Check(PowrProf.PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref id, out uint pluggedIn), "Couldn't read the power setting " + setting.Key + ".");
            Check(PowrProf.PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref id, out uint onBattery), "Couldn't read the power setting " + setting.Key + ".");
            return new PowerValues(pluggedIn, onBattery);
        }

        public void Write(PowerSetting setting, PowerValues values)
        {
            Guid scheme = ActiveScheme();
            Guid subgroup = setting.Subgroup;
            Guid id = setting.Setting;
            Check(PowrProf.PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref id, values.PluggedIn), "Couldn't change the power setting " + setting.Key + ".");
            Check(PowrProf.PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref id, values.OnBattery), "Couldn't change the power setting " + setting.Key + ".");
        }

        /// <summary>Windows only acts on changed values once the plan is made active again.</summary>
        public void Activate()
        {
            Guid scheme = ActiveScheme();
            Check(PowrProf.PowerSetActiveScheme(IntPtr.Zero, ref scheme), "Couldn't apply the changed power plan.");
        }

        private static Guid ActiveScheme()
        {
            Check(PowrProf.PowerGetActiveScheme(IntPtr.Zero, out IntPtr scheme), "Couldn't find the active power plan.");
            try
            {
                return Marshal.PtrToStructure<Guid>(scheme);
            }
            finally
            {
                Kernel32.LocalFree(scheme);
            }
        }

        private static void Check(uint result, string failure)
        {
            if (result != PowrProf.Success)
            {
                throw new Win32Exception((int)result, failure);
            }
        }
    }
}
