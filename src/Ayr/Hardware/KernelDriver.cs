using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Ayr.App;
using Ayr.Interop;

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

        private readonly string _serviceName;
        private readonly string _devicePath;
        private readonly VendorLibrary _file;
        private readonly object _startLock = new object();

        private KernelDriver(string serviceName, string devicePath, VendorLibrary file)
        {
            _serviceName = serviceName;
            _devicePath = devicePath;
            _file = file;
        }

        public bool IsRunning => DeviceAccess.Exists(_devicePath);

        public void EnsureRunning()
        {
            lock (_startLock)
            {
                if (!DeviceAccess.Exists(_devicePath))
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
            using (var manager = new ServiceControlHandle(Advapi32.OpenSCManager(null, null, Advapi32.ScManagerAllAccess)))
            using (var service = new ServiceControlHandle(Advapi32.OpenService(manager.Value, _serviceName, Advapi32.ServiceAllAccess)))
            {
                if (service.IsInvalid || !ImagePathIsOurs())
                {
                    return false;
                }
                if (!Advapi32.DeleteService(service.Value))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not remove the " + _serviceName + " service.");
                }
                ActivityLog.Write(_serviceName + " service removed; the driver unloads at the next restart");
                return true;
            }
        }

        private void Start(string driverPath)
        {
            using (var manager = new ServiceControlHandle(Advapi32.OpenSCManager(null, null, Advapi32.ScManagerAllAccess)))
            {
                if (manager.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the service manager (needs administrator rights).");
                }
                using (ServiceControlHandle service = RegisterService(manager, driverPath))
                {
                    if (!Advapi32.StartService(service.Value, 0, IntPtr.Zero))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == Advapi32.ErrorServiceMarkedForDelete)
                        {
                            throw new InvalidOperationException("The " + _serviceName + " driver is being removed; restart Windows, then try again.");
                        }
                        if (error != Advapi32.ErrorServiceAlreadyRunning)
                        {
                            throw new Win32Exception(error, "Could not start the " + _serviceName + " driver.");
                        }
                    }
                }
            }
            ActivityLog.Write(_serviceName + " driver started from " + driverPath);
        }

        private ServiceControlHandle RegisterService(ServiceControlHandle manager, string driverPath)
        {
            var service = new ServiceControlHandle(Advapi32.OpenService(manager.Value, _serviceName, Advapi32.ServiceAllAccess));
            if (service.IsInvalid)
            {
                service.Dispose();
                service = new ServiceControlHandle(Advapi32.CreateService(manager.Value, _serviceName, _serviceName, Advapi32.ServiceAllAccess,
                    Advapi32.ServiceKernelDriver, Advapi32.ServiceDemandStart, Advapi32.ServiceErrorNormal, driverPath,
                    null, IntPtr.Zero, null, null, null));
                if (service.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the " + _serviceName + " driver.");
                }
                ActivityLog.Write(_serviceName + " service registered");
                return service;
            }
            // An existing entry may be disabled or point at a removed MSI install; it is pointed at Ayr's copy instead.
            if (!Advapi32.ChangeServiceConfig(service.Value, Advapi32.ServiceNoChange, Advapi32.ServiceDemandStart, Advapi32.ServiceNoChange,
                driverPath, null, IntPtr.Zero, null, null, null, null))
            {
                int error = Marshal.GetLastWin32Error();
                service.Dispose();
                throw new Win32Exception(error, "Could not update the " + _serviceName + " service.");
            }
            return service;
        }

        private bool ImagePathIsOurs()
        {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + _serviceName))
            {
                string imagePath = key?.GetValue("ImagePath") as string;
                return imagePath != null && imagePath.IndexOf(AppPaths.VendorDirectory, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
