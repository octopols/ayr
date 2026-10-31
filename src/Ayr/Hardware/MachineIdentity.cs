using Microsoft.Win32;

namespace Ayr.Hardware
{
    public static class MachineIdentity
    {
        private const string BiosKey = @"HARDWARE\DESCRIPTION\System\BIOS";
        private const string Unknown = "MSI laptop";

        /// <summary>The model name the firmware reports, e.g. "GF65 Thin 10SDR".</summary>
        public static string ModelName
        {
            get
            {
                using (RegistryKey bios = Registry.LocalMachine.OpenSubKey(BiosKey))
                {
                    string model = (bios?.GetValue("SystemProductName") as string)?.Trim();
                    return string.IsNullOrEmpty(model) ? Unknown : model;
                }
            }
        }
    }
}
