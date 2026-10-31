using System.Collections.Generic;
using Ayr.App;
using Ayr.Apple;

namespace Ayr.Ui
{
    /// <summary>Costlier readings (device queries, the scheduled task) refreshed rarely or after a change.</summary>
    public sealed class SlowState
    {
        public SlowState(DesiredState desired, bool appleChargerInstalled, IReadOnlyList<AppleDevice> appleDevices, bool? autostartOn)
        {
            Desired = desired;
            AppleChargerInstalled = appleChargerInstalled;
            AppleDevices = appleDevices;
            AutostartOn = autostartOn;
        }

        public DesiredState Desired { get; }
        public bool AppleChargerInstalled { get; }
        public IReadOnlyList<AppleDevice> AppleDevices { get; }

        /// <summary>Null when it was not read this time.</summary>
        public bool? AutostartOn { get; }
    }
}
