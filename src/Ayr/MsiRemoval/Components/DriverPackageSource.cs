using System;
using System.Collections.Generic;
using System.Management;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// MSI's Windows software components, and nothing else from the driver store: MSI also publishes the BIOS firmware
    /// package and customises audio drivers, and removing either would break the laptop.
    /// </summary>
    public sealed class DriverPackageSource : IMsiComponentSource
    {
        private const string SoftwareComponentClass = "SOFTWARECOMPONENT";
        private const string Query = "SELECT InfName, DeviceName, DriverProviderName, DeviceClass FROM Win32_PnPSignedDriver";

        public IEnumerable<MsiComponent> Find()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var searcher = new ManagementObjectSearcher(Query))
            using (ManagementObjectCollection drivers = searcher.Get())
            {
                foreach (ManagementBaseObject driver in drivers)
                {
                    using (driver)
                    {
                        string infName = driver["InfName"] as string;
                        bool isMsiComponent = SoftwareComponentClass.Equals(driver["DeviceClass"] as string, StringComparison.OrdinalIgnoreCase)
                            && MicroStar.IsPublisher(driver["DriverProviderName"] as string);
                        if (isMsiComponent && infName != null && seen.Add(infName))
                        {
                            yield return new DriverPackage(infName, driver["DeviceName"] as string ?? infName);
                        }
                    }
                }
            }
        }
    }
}
