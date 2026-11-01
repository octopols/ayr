using System;
using System.Windows.Forms;

namespace Ayr.Battery
{
    /// <summary>The battery's charge and where the laptop draws power from, as Windows reports them right now.</summary>
    public sealed class BatteryReading
    {
        private BatteryReading(int percent, bool onCharger, bool charging)
        {
            Percent = percent;
            OnCharger = onCharger;
            Charging = charging;
        }

        public int Percent { get; }
        public bool OnCharger { get; }
        public bool Charging { get; }

        /// <summary>On the charger but neither charging nor discharging: the firmware considers the battery full.</summary>
        public bool Idle => OnCharger && !Charging;

        public static BatteryReading Now()
        {
            PowerStatus status = SystemInformation.PowerStatus;
            if ((status.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0 || status.BatteryChargeStatus == BatteryChargeStatus.Unknown)
            {
                throw new InvalidOperationException("Windows can't see the battery.");
            }
            return new BatteryReading(
                (int)Math.Round(status.BatteryLifePercent * 100),
                status.PowerLineStatus == PowerLineStatus.Online,
                (status.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0);
        }
    }
}
