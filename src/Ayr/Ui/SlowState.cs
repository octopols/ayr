using System;
using System.Collections.Generic;
using Ayr.App;
using Ayr.Apple;

namespace Ayr.Ui
{
    /// <summary>Costlier readings (device queries, services, the scheduled task) refreshed rarely or after a change.</summary>
    public sealed class SlowState
    {
        public SlowState(DesiredState desired, IReadOnlyList<AppleDeviceStatus> appleDevices, DateTime nextAppleRequestAllowedAt,
            bool? autostartOn, bool msiCenterRunning)
        {
            Desired = desired;
            AppleDevices = appleDevices;
            NextAppleRequestAllowedAt = nextAppleRequestAllowedAt;
            AutostartOn = autostartOn;
            MsiCenterRunning = msiCenterRunning;
        }

        public DesiredState Desired { get; }
        public IReadOnlyList<AppleDeviceStatus> AppleDevices { get; }
        public DateTime NextAppleRequestAllowedAt { get; }

        /// <summary>Null when it was not read this time.</summary>
        public bool? AutostartOn { get; }

        public bool MsiCenterRunning { get; }
    }
}
