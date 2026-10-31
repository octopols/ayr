using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Ayr.App;
using Ayr.Interop;
using Microsoft.Win32.SafeHandles;

namespace Ayr.Hardware
{
    /// <summary>
    /// MSI's WINIO kernel driver (KernCoreLib64.sys), the only way to reach EC RAM, which sits at a physical address. Ayr
    /// registers it as a demand-start service under the name MSI uses, so an existing MSI install keeps working, and starts
    /// it when needed.
    ///
    /// The driver creates its device without a security descriptor, which lets any program open it and map physical
    /// memory. Ayr therefore replaces the device's access list with SYSTEM and Administrators only every time it starts the
    /// driver or finds it running.
    /// </summary>
    public static class WinIoDriver
    {
        private const string ServiceName = "WINIO";
        private const string DevicePath = @"\\.\WINIO";
        private const string AdministratorsAndSystemOnly = "D:P(A;;GA;;;SY)(A;;GA;;;BA)";
        private const uint WriteDac = 0x00040000;
        private const uint ReadControl = 0x00020000;
        private const uint OpenExisting = 3;
        private const int ErrorFileNotFound = 2;

        private static readonly object StartLock = new object();

        public static void EnsureRunning()
        {
            lock (StartLock)
            {
                if (!DeviceExists())
                {
                    Start(VendorLibrary.EcDriver.Locate());
                }
                RestrictAccess();
            }
        }

        /// <summary>
        /// Removes the service if it points at Ayr's own copy of the driver. The driver is left loaded until the next reboot
        /// rather than stopped while in use.
        /// </summary>
        public static bool RemoveIfOwned()
        {
            using (var manager = new ServiceControlHandle(Advapi32.OpenSCManager(null, null, Advapi32.ScManagerAllAccess)))
            using (var service = new ServiceControlHandle(Advapi32.OpenService(manager.Value, ServiceName, Advapi32.ServiceAllAccess)))
            {
                if (service.IsInvalid || !ImagePathIsOurs())
                {
                    return false;
                }
                if (!Advapi32.DeleteService(service.Value))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not remove the WINIO service.");
                }
                ActivityLog.Write("WINIO service removed; the driver unloads at the next reboot");
                return true;
            }
        }

        private static void Start(string driverPath)
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
                            throw new InvalidOperationException("The WINIO driver is being removed; restart Windows, then start Ayr again.");
                        }
                        if (error != Advapi32.ErrorServiceAlreadyRunning)
                        {
                            throw new Win32Exception(error, "Could not start the WINIO driver.");
                        }
                    }
                }
            }
            ActivityLog.Write("WINIO driver started from " + driverPath);
        }

        private static ServiceControlHandle RegisterService(ServiceControlHandle manager, string driverPath)
        {
            var service = new ServiceControlHandle(Advapi32.OpenService(manager.Value, ServiceName, Advapi32.ServiceAllAccess));
            if (service.IsInvalid)
            {
                service.Dispose();
                service = new ServiceControlHandle(Advapi32.CreateService(manager.Value, ServiceName, ServiceName, Advapi32.ServiceAllAccess,
                    Advapi32.ServiceKernelDriver, Advapi32.ServiceDemandStart, Advapi32.ServiceErrorNormal, driverPath,
                    null, IntPtr.Zero, null, null, null));
                if (service.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the WINIO driver.");
                }
                ActivityLog.Write("WINIO service registered");
                return service;
            }
            // An existing entry may be disabled or point at a removed MSI install; it is pointed at Ayr's copy instead.
            if (!Advapi32.ChangeServiceConfig(service.Value, Advapi32.ServiceNoChange, Advapi32.ServiceDemandStart, Advapi32.ServiceNoChange,
                driverPath, null, IntPtr.Zero, null, null, null, null))
            {
                int error = Marshal.GetLastWin32Error();
                service.Dispose();
                throw new Win32Exception(error, "Could not update the WINIO service.");
            }
            return service;
        }

        private static bool DeviceExists()
        {
            using (SafeFileHandle device = OpenDevice(ReadControl))
            {
                if (!device.IsInvalid)
                {
                    return true;
                }
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorFileNotFound)
                {
                    return false;
                }
                throw new Win32Exception(error, "Could not open the WINIO device.");
            }
        }

        private static void RestrictAccess()
        {
            if (!Advapi32.ConvertStringSecurityDescriptorToSecurityDescriptor(AdministratorsAndSystemOnly, Advapi32.SddlRevision1,
                out IntPtr descriptor, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not build the WINIO access list.");
            }
            try
            {
                using (SafeFileHandle device = OpenDevice(WriteDac | ReadControl))
                {
                    if (device.IsInvalid)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the WINIO device to secure it.");
                    }
                    uint information = Advapi32.DaclSecurityInformation | Advapi32.ProtectedDaclSecurityInformation;
                    if (!Advapi32.SetKernelObjectSecurity(device, information, descriptor))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not restrict access to the WINIO device.");
                    }
                }
            }
            finally
            {
                Kernel32.LocalFree(descriptor);
            }
        }

        private static bool ImagePathIsOurs()
        {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + ServiceName))
            {
                string imagePath = key?.GetValue("ImagePath") as string;
                return imagePath != null && imagePath.IndexOf(AppPaths.VendorDirectory, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private static SafeFileHandle OpenDevice(uint access) =>
            Kernel32.CreateFile(DevicePath, access, FileShare.ReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
    }
}
