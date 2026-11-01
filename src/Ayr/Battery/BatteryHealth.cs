using System;
using System.Linq;
using System.Management;
using Ayr.Hardware;

namespace Ayr.Battery
{
    /// <summary>
    /// How much charge the battery holds now against what it was made to hold. The design figure comes from MSI's battery
    /// block (capacity in mAh at index 1, voltage in mV at index 2), the current one from Windows; both match Windows'
    /// own battery report. The battery estimates its current capacity itself, so a miscalibrated one gets it wrong.
    /// </summary>
    public sealed class BatteryHealth
    {
        /// <summary>Below this, a worn battery can't power the laptop through a calibration's emptying step.</summary>
        public const int MinimumForCalibration = 40;

        private const int DesignCapacityIndex = 1;
        private const int DesignVoltageIndex = 2;
        private const int MillivoltsPerVolt = 1000;

        private BatteryHealth(int designCapacityMwh, int fullChargeCapacityMwh)
        {
            DesignCapacityMwh = designCapacityMwh;
            FullChargeCapacityMwh = fullChargeCapacityMwh;
        }

        public int DesignCapacityMwh { get; }
        public int FullChargeCapacityMwh { get; }

        public int Percent => (int)Math.Round(100.0 * FullChargeCapacityMwh / DesignCapacityMwh);

        public bool CanCalibrate => Percent >= MinimumForCalibration;

        public static BatteryHealth Read(WmiBlockClient wmi)
        {
            long designMilliampHours = wmi.ReadNumber(WmiBlock.MasterBattery, DesignCapacityIndex);
            long designMillivolts = wmi.ReadNumber(WmiBlock.MasterBattery, DesignVoltageIndex);
            int design = (int)(designMilliampHours * designMillivolts / MillivoltsPerVolt);
            if (design <= 0)
            {
                throw new InvalidOperationException("The battery didn't report its design capacity.");
            }
            return new BatteryHealth(design, FullChargeCapacity());
        }

        private static int FullChargeCapacity()
        {
            using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                ManagementBaseObject battery = results.Cast<ManagementBaseObject>().FirstOrDefault()
                    ?? throw new InvalidOperationException("Windows didn't report the battery's capacity.");
                using (battery)
                {
                    return Convert.ToInt32(battery["FullChargedCapacity"]);
                }
            }
        }
    }
}
