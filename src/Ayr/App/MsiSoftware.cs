using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace Ayr.App
{
    /// <summary>MSI services that act on the same hardware as Ayr when they are still installed and running.</summary>
    public static class MsiSoftware
    {
        private const string MsiCenterService = "MSI_Central_Service";
        private const string SuperChargerService = "MSI_SuperCharger";
        private const string KeyBrokerProcess = "OmApSvcBroker";

        /// <summary>MSI Center re-applies its own fan and performance profile after sleep and on power changes.</summary>
        public static bool IsMsiCenterRunning => IsRunning(MsiCenterService);

        /// <summary>Super Charger's service sends its own Apple fast-charge requests.</summary>
        public static bool IsSuperChargerRunning => IsRunning(SuperChargerService);

        /// <summary>
        /// MSI's key broker opens Dragon Center on Fn+F7, and MSI Center listens for the same key. While either runs, Ayr
        /// answering the key as well would open two apps at once.
        /// </summary>
        public static bool UsesFnF7 => IsProcessRunning(KeyBrokerProcess) || IsMsiCenterRunning;

        private static bool IsProcessRunning(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
            {
                process.Dispose();
            }
            return processes.Length > 0;
        }

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
