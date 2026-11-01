using System;
using System.Globalization;
using Microsoft.Win32;

namespace Ayr.Hardware
{
    /// <summary>What the firmware says about the machine, read from the registry copy of its SMBIOS strings.</summary>
    public static class MachineIdentity
    {
        private const string BiosKey = @"HARDWARE\DESCRIPTION\System\BIOS";
        private const string FallbackName = "MSI laptop";
        private const string MsiManufacturer = "Micro-Star";
        private const string FirmwareDateFormat = "MM/dd/yyyy";

        /// <summary>The model name the firmware reports, e.g. "GF65 Thin 10SDR".</summary>
        public static string ModelName
        {
            get
            {
                string model = BiosValue("SystemProductName");
                return string.IsNullOrEmpty(model) ? FallbackName : model;
            }
        }

        public static bool IsMsi => BiosValue("SystemManufacturer").IndexOf(MsiManufacturer, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>When the firmware was released; null when it doesn't say.</summary>
        public static DateTime? FirmwareDate =>
            DateTime.TryParseExact(BiosValue("BIOSReleaseDate"), FirmwareDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
                ? date
                : (DateTime?)null;

        private static string BiosValue(string name)
        {
            using (RegistryKey bios = Registry.LocalMachine.OpenSubKey(BiosKey))
            {
                return (bios?.GetValue(name) as string)?.Trim() ?? "";
            }
        }
    }
}
