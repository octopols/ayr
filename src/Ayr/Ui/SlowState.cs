using System;
using System.Collections.Generic;
using Ayr.App;
using Ayr.Apple;
using Ayr.Games;

namespace Ayr.Ui
{
    /// <summary>Costlier readings (device queries, services, the scheduled task) refreshed rarely or after a change.</summary>
    public sealed class SlowState
    {
        public SlowState(DesiredState desired, CurveTemperatures curveTemperatures, int? chargeLimitPercent, GameOverview games,
            IReadOnlyList<AppleDeviceStatus> appleDevices, DateTime nextAppleRequestAllowedAt, bool? autostartOn, bool msiCenterRunning,
            FnKeyBindings fnKeys)
        {
            Desired = desired;
            CurveTemperatures = curveTemperatures;
            ChargeLimitPercent = chargeLimitPercent;
            Games = games;
            AppleDevices = appleDevices;
            NextAppleRequestAllowedAt = nextAppleRequestAllowedAt;
            AutostartOn = autostartOn;
            MsiCenterRunning = msiCenterRunning;
            FnKeys = fnKeys;
        }

        public DesiredState Desired { get; }
        public CurveTemperatures CurveTemperatures { get; }

        /// <summary>The battery's current limit; null when EC RAM can't be reached.</summary>
        public int? ChargeLimitPercent { get; }

        public GameOverview Games { get; }
        public IReadOnlyList<AppleDeviceStatus> AppleDevices { get; }
        public DateTime NextAppleRequestAllowedAt { get; }

        /// <summary>Null when it was not read this time.</summary>
        public bool? AutostartOn { get; }

        public bool MsiCenterRunning { get; }
        public FnKeyBindings FnKeys { get; }
    }
}
