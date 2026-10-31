using System;
using System.ServiceProcess;

namespace Ayr.App
{
    /// <summary>MSI services that act on the same hardware as Ayr when they are still installed and running.</summary>
    public static class MsiSoftware
    {
        private const string MsiCenterService = "MSI_Central_Service";
        private const string SuperChargerService = "MSI_SuperCharger";

        /// <summary>MSI Center re-applies its own fan and performance profile after sleep and on power changes.</summary>
        public static bool IsMsiCenterRunning => IsRunning(MsiCenterService);

        /// <summary>Super Charger's service sends its own Apple fast-charge requests.</summary>
        public static bool IsSuperChargerRunning => IsRunning(SuperChargerService);

        private static bool IsRunning(string serviceName)
        {
            using (var service = new ServiceController(serviceName))
            {
                try
                {
                    return service.Status == ServiceControllerStatus.Running;
                }
                catch (InvalidOperationException)
                {
                    // Not installed.
                    return false;
                }
            }
        }
    }
}
