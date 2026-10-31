using System;
using System.Management;
using Ayr.App;
using Microsoft.Win32;

namespace Ayr.Hardware
{
    /// <summary>
    /// MSI's ACPI-WMI classes (MSI_System, MSI_CPU, ...). The firmware exposes the data, but Windows only knows the class
    /// definitions from MSI's msiapcfg.dll, registered as the MOF image of Windows' own wmiacpi driver. MSI's software
    /// installs it; Ayr ships the same signed file and registers it when an MSI laptop doesn't have it.
    /// </summary>
    public static class FirmwareInterface
    {
        private const string WmiAcpiKey = @"SYSTEM\CurrentControlSet\Services\WmiAcpi";
        private const string MofImagePath = "MofImagePath";
        private const string ProbeClass = "root\\WMI:MSI_System";

        /// <summary>Registers Ayr's copy of msiapcfg.dll when the classes are missing; Windows loads it at the next restart.</summary>
        public static FirmwareInterfaceState EnsureRegistered()
        {
            if (ClassesExist())
            {
                return FirmwareInterfaceState.Ready;
            }
            if (!MachineIdentity.IsMsi)
            {
                return FirmwareInterfaceState.NotAnMsiLaptop;
            }
            string definitions = VendorLibrary.FirmwareInterface.Locate();
            using (RegistryKey wmiAcpi = Registry.LocalMachine.OpenSubKey(WmiAcpiKey, writable: true))
            {
                wmiAcpi.SetValue(MofImagePath, definitions, RegistryValueKind.ExpandString);
            }
            ActivityLog.Write("registered " + definitions + " as wmiacpi's MOF image; restart required");
            return FirmwareInterfaceState.RestartRequired;
        }

        /// <summary>Removes the registration if it points at Ayr's own copy.</summary>
        public static bool RemoveIfOwned()
        {
            using (RegistryKey wmiAcpi = Registry.LocalMachine.OpenSubKey(WmiAcpiKey, writable: true))
            {
                string current = wmiAcpi?.GetValue(MofImagePath, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if (current == null || current.IndexOf(AppPaths.VendorDirectory, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
                wmiAcpi.DeleteValue(MofImagePath);
                ActivityLog.Write("removed Ayr's msiapcfg.dll registration");
                return true;
            }
        }

        private static bool ClassesExist()
        {
            using (var probe = new ManagementClass(ProbeClass))
            {
                try
                {
                    probe.Get();
                    return true;
                }
                catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound || ex.ErrorCode == ManagementStatus.InvalidClass)
                {
                    return false;
                }
            }
        }
    }
}
