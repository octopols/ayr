using System.Runtime.InteropServices;
using Ayr.Interop;

namespace Ayr.App
{
    /// <summary>
    /// Windows "efficiency mode" (EcoQoS). It only lowers the process's clock and power preference; the priority class is
    /// left at Normal because this process hosts the Silent thermal guard, which a game at 100% CPU must not starve.
    /// </summary>
    public static class EfficiencyMode
    {
        public static void Enter()
        {
            Set(true);
            WorkingSet.Trim();
        }

        public static void Leave() => Set(false);

        private static void Set(bool throttled)
        {
            var state = new Kernel32.PowerThrottlingState
            {
                Version = Kernel32.PowerThrottlingCurrentVersion,
                ControlMask = Kernel32.PowerThrottlingExecutionSpeed,
                StateMask = throttled ? Kernel32.PowerThrottlingExecutionSpeed : 0
            };
            // Fails harmlessly on Windows builds without EcoQoS.
            Kernel32.SetProcessInformation(Kernel32.GetCurrentProcess(), Kernel32.ProcessPowerThrottling, ref state,
                Marshal.SizeOf(typeof(Kernel32.PowerThrottlingState)));
        }
    }
}
