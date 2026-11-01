using System;

namespace Ayr.Hardware
{
    /// <summary>
    /// The firmware switch MSI's battery calibration uses (MSIBatteryCalibration.exe, SetDCAdapter): while it is set, the
    /// laptop runs on its battery even with the charger plugged in, so the battery can be emptied without unplugging.
    /// It is MSI_AP instance 0: 0 powers the laptop from the charger, 1 from the battery.
    /// </summary>
    public sealed class ChargerSwitch
    {
        public const int Index = 0;
        private const byte UseCharger = 0;
        private const byte UseBattery = 1;

        private readonly WmiBlockClient _wmi;

        public ChargerSwitch(WmiBlockClient wmi)
        {
            _wmi = wmi;
        }

        public bool IsRunningOnBattery => _wmi.ReadByte(WmiBlock.Ap, Index) != UseCharger;

        /// <summary>Whether the firmware exposes the switch; laptops without MSI's WMI interface don't.</summary>
        public bool IsAvailable
        {
            get
            {
                try
                {
                    _wmi.ReadByte(WmiBlock.Ap, Index);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
            }
        }

        public void RunOnBattery() => _wmi.WriteByte(WmiBlock.Ap, Index, UseBattery);

        public void RunOnCharger() => _wmi.WriteByte(WmiBlock.Ap, Index, UseCharger);
    }
}
