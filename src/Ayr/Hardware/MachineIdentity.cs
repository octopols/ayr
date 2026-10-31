using System;
using Microsoft.Win32;

namespace Ayr.Hardware
{
    /// <summary>What the firmware says about the machine, read from the registry copy of its SMBIOS strings.</summary>
    public static class MachineIdentity
    {
        private const string BiosKey = @"HARDWARE\DESCRIPTION\System\BIOS";
        private const string FallbackName = "MSI laptop";
        private const string MsiManufacturer = "Micro-Star";

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

        private static string BiosValue(string name)
        {
            using (RegistryKey bios = Registry.LocalMachine.OpenSubKey(BiosKey))
            {
                return (bios?.GetValue(name) as string)?.Trim() ?? "";
            }
        }
    }
}
