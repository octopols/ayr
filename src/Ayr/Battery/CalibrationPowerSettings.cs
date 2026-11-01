using System;
using System.Collections.Generic;

namespace Ayr.Battery
{
    /// <summary>
    /// The power plan changes MSI's calibration makes for its run (MSIBatteryCalibration.exe, ChangeDisplayPolicy and
    /// ChangePowerPolicy): nothing may sleep, hibernate or act on a closed lid, and Windows' low-battery levels move below
    /// the 5% the battery is emptied to, with no action at the critical level, so Windows doesn't hibernate first.
    /// </summary>
    public static class CalibrationPowerSettings
    {
        private const uint Never = 0;
        private const uint DoNothing = 0;

        private static readonly Guid Display = new Guid("7516b95f-f776-4464-8c53-06167f40cc99");
        private static readonly Guid Sleep = new Guid("238c9fa8-0aad-41ed-83f4-97be242c8f20");
        private static readonly Guid Disk = new Guid("0012ee47-9041-4b5d-9b77-535fba8b1442");
        private static readonly Guid Buttons = new Guid("4f971e89-eebd-4455-a8de-9e59040e7347");
        private static readonly Guid Battery = new Guid("e73a048d-bf27-4f12-9731-8b2076e8891f");

        public static IReadOnlyList<PowerOverride> All { get; } = new[]
        {
            Both("display_off", Display, "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", Never),
            Both("display_dim", Display, "17aaa29b-8b43-4b94-aafe-35f64daaf1ee", Never),
            Both("sleep", Sleep, "29f6c1db-86da-48c5-9fdb-f2b67b1f44da", Never),
            Both("hibernate", Sleep, "9d7815a6-7ee4-497e-8888-515a05f02364", Never),
            Both("disk_off", Disk, "6738e2c4-e8a5-4a42-b16a-e040e769756e", Never),
            Both("lid", Buttons, "5ca83367-6e45-459f-a27b-476b1d01c936", DoNothing),
            Both("critical_action", Battery, "637ea02f-bbcb-4015-8e2c-a1c7b9c0b546", DoNothing),
            OnBattery("critical_level", Battery, "9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469", 2),
            OnBattery("low_level", Battery, "8183ba9a-e910-48da-8769-14ae6dc1170a", 4),
            OnBattery("reserve_level", Battery, "f3c5027d-cd16-4930-aa6b-90db844a8f00", 3)
        };

        private static PowerOverride Both(string key, Guid subgroup, string setting, uint value) =>
            new PowerOverride(new PowerSetting(key, subgroup, new Guid(setting)), value, value);

        private static PowerOverride OnBattery(string key, Guid subgroup, string setting, uint value) =>
            new PowerOverride(new PowerSetting(key, subgroup, new Guid(setting)), null, value);
    }
}
