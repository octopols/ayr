using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Ayr.App;
using Ayr.Interop;
using Microsoft.Win32;

namespace Ayr.Hardware
{
    /// <summary>
    /// One of the two MSI kernel drivers Ayr uses, registered as a demand-start service under the name MSI uses, so an
    /// existing MSI install keeps working, and started from Ayr's own copy when no running copy exists. Both drivers let
    /// whoever opens their device do privileged things, so the device is restricted to administrators every time Ayr starts
    /// the driver or finds it running. Ayr never stops either driver; stopping MSI's Apple driver has crashed Windows.
    /// </summary>
    public sealed class KernelDriver
    {
        /// <summary>WINIO: maps physical memory (EC RAM) and writes I/O ports.</summary>
        public static readonly KernelDriver EcAccess = new KernelDriver("WINIO", @"\\.\WINIO", VendorLibrary.EcDriver);

        /// <summary>ipadtst2: sends the Apple fast-charge request to USB devices.</summary>
        public static readonly KernelDriver AppleCharge = new KernelDriver("ipadtst2", @"\\.\IpadtstSamp2", VendorLibrary.AppleChargeDriver);

        private readonly string _devicePath;
        private readonly VendorLibrary _file;
        private readonly object _startLock = new object();

        private KernelDriver(string serviceName, string devicePath, VendorLibrary file)
        {
            ServiceName = serviceName;
            _devicePath = devicePath;
            _file = file;
        }

        public string ServiceName { get; }

        public bool IsRunning => DeviceAccess.Exists(_devicePath);

        public void EnsureRunning()
        {
            lock (_startLock)
            {
                if (!IsRunning)
                {
                    Start(_file.Locate());
                }
                DeviceAccess.RestrictToAdministrators(_devicePath);
            }
        }

        /// <summary>
        /// Removes the service if it points at Ayr's own copy of the driver. The driver stays loaded until the next restart
        /// rather than being stopped while in use.
        /// </summary>
        public bool RemoveIfOwned()
        {
            if (!ImagePathIsOurs())
            {
                return false;
            }
            using (ServiceControlHandle manager = OpenServiceManager())
            using (ServiceControlHandle service = Advapi32.OpenService(manager, ServiceName, Advapi32.ServiceAllAccess))
            {
                if (service.IsInvalid || !Advapi32.DeleteService(service))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not remove the " + ServiceName + " service.");
                }
            }
            ActivityLog.Write(ServiceName + " service removed; the driver unloads at the next restart");
            return true;
        }

        private void Start(string driverPath)
        {
            using (ServiceControlHandle manager = OpenServiceManager())
            using (ServiceControlHandle service = OpenOrCreateService(manager, driverPath))
            {
                if (!Advapi32.StartService(service, 0, IntPtr.Zero))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == Advapi32.ErrorServiceMarkedForDelete)
                    {
                        throw new InvalidOperationException("The " + ServiceName + " driver is being removed; restart Windows, then try again.");
                    }
                    if (error != Advapi32.ErrorServiceAlreadyRunning)
                    {
                        throw new Win32Exception(error, "Could not start the " + ServiceName + " driver.");
                    }
                }
            }
            ActivityLog.Write(ServiceName + " driver started from " + driverPath);
        }

        // An existing entry may be disabled or point at a removed MSI install, so it is pointed at Ayr's copy.
        private ServiceControlHandle OpenOrCreateService(ServiceControlHandle manager, string driverPath)
        {
            ServiceControlHandle service = Advapi32.OpenService(manager, ServiceName, Advapi32.ServiceAllAccess);
            if (service.IsInvalid)
            {
                service.Dispose();
                return CreateService(manager, driverPath);
            }
            if (!Advapi32.ChangeServiceConfig(service, Advapi32.ServiceNoChange, Advapi32.ServiceDemandStart, Advapi32.ServiceNoChange,
                driverPath, null, IntPtr.Zero, null, null, null, null))
            {
                int error = Marshal.GetLastWin32Error();
                service.Dispose();
                throw new Win32Exception(error, "Could not update the " + ServiceName + " service.");
            }
            return service;
        }

        private ServiceControlHandle CreateService(ServiceControlHandle manager, string driverPath)
        {
            ServiceControlHandle service = Advapi32.CreateService(manager, ServiceName, ServiceName, Advapi32.ServiceAllAccess,
                Advapi32.ServiceKernelDriver, Advapi32.ServiceDemandStart, Advapi32.ServiceErrorNormal, driverPath,
                null, IntPtr.Zero, null, null, null);
            if (service.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                service.Dispose();
                throw new Win32Exception(error, "Could not register the " + ServiceName + " driver.");
            }
            ActivityLog.Write(ServiceName + " service registered");
            return service;
        }

        private static ServiceControlHandle OpenServiceManager()
        {
            ServiceControlHandle manager = Advapi32.OpenSCManager(null, null, Advapi32.ScManagerAllAccess);
            if (manager.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                manager.Dispose();
                throw new Win32Exception(error, "Could not open the service manager (needs administrator rights).");
            }
            return manager;
        }

        private bool ImagePathIsOurs()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + ServiceName))
            {
                string imagePath = key?.GetValue("ImagePath") as string;
                return imagePath != null && imagePath.IndexOf(AppPaths.VendorDirectory, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
