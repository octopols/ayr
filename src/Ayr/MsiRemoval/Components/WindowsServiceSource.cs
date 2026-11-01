using System;
using System.Collections.Generic;
using System.Linq;
using Ayr.Hardware;
using Microsoft.Win32;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// Services and drivers whose program is MSI's. The two driver entries Ayr itself uses are left out: Ayr points them at
    /// its own copies before anything is removed.
    /// </summary>
    public sealed class WindowsServiceSource : IMsiComponentSource
    {
        private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";
        private const int KernelDriverType = 0x1;
        private const int FileSystemDriverType = 0x2;

        private static readonly string[] AyrDrivers = { KernelDriver.EcAccess.ServiceName, KernelDriver.AppleCharge.ServiceName };

        // MsIo64.sys, the I/O driver MSI installs with Mystic Light, lives in System32 and may not name MSI as its maker.
        private static readonly string[] KnownMsiServices = { "MSIO" };

        public IEnumerable<MsiComponent> Find()
        {
            using (RegistryKey services = Registry.LocalMachine.OpenSubKey(ServicesKey))
            {
                foreach (string name in services.GetSubKeyNames().Where(name => !AyrDrivers.Contains(name, StringComparer.OrdinalIgnoreCase)))
                {
                    using (RegistryKey service = services.OpenSubKey(name))
                    {
                        WindowsService found = Read(name, service);
                        if (found != null)
                        {
                            yield return found;
                        }
                    }
                }
            }
        }

        private static WindowsService Read(string name, RegistryKey service)
        {
            string binary = BinaryOf(service);
            if (binary == null || MicroStar.IsAyrPath(binary))
            {
                return null;
            }
            bool known = KnownMsiServices.Contains(name, StringComparer.OrdinalIgnoreCase);
            if (!known && !MicroStar.OwnsFile(binary))
            {
                return null;
            }
            int type = service.GetValue("Type") is int value ? value : 0;
            bool isDriver = (type & (KernelDriverType | FileSystemDriverType)) != 0;
            string displayName = service.GetValue("DisplayName") as string;
            return new WindowsService(isDriver ? MsiComponentKind.Driver : MsiComponentKind.Service, name,
                string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith("@") ? name : displayName, binary);
        }

        // Services hosted in svchost name their real program as ServiceDll.
        private static string BinaryOf(RegistryKey service)
        {
            string imagePath = service?.GetValue("ImagePath") as string;
            if (imagePath == null)
            {
                return null;
            }
            using (RegistryKey parameters = service.OpenSubKey("Parameters"))
            {
                string serviceDll = parameters?.GetValue("ServiceDll") as string;
                return CommandPath.ProgramOf(serviceDll ?? imagePath);
            }
        }
    }
}
