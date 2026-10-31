using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Ayr.Interop;
using Microsoft.Win32.SafeHandles;

namespace Ayr.Hardware
{
    /// <summary>
    /// MSI's drivers create their devices without a security descriptor, so any program could open them. Ayr replaces the
    /// device's access list with SYSTEM and Administrators only. The list lasts until the driver is unloaded.
    /// </summary>
    public static class DeviceAccess
    {
        private const string AdministratorsAndSystemOnly = "D:P(A;;GA;;;SY)(A;;GA;;;BA)";
        private const uint WriteDac = 0x00040000;
        private const uint ReadControl = 0x00020000;
        private const uint OpenExisting = 3;
        private const int ErrorFileNotFound = 2;

        public static bool Exists(string devicePath)
        {
            using (SafeFileHandle device = Open(devicePath, ReadControl))
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
                throw new Win32Exception(error, "Could not open " + devicePath + ".");
            }
        }

        public static void RestrictToAdministrators(string devicePath)
        {
            if (!Advapi32.ConvertStringSecurityDescriptorToSecurityDescriptor(AdministratorsAndSystemOnly, Advapi32.SddlRevision1,
                out IntPtr descriptor, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not build the device access list.");
            }
            try
            {
                using (SafeFileHandle device = Open(devicePath, WriteDac | ReadControl))
                {
                    if (device.IsInvalid)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open " + devicePath + " to secure it.");
                    }
                    uint information = Advapi32.DaclSecurityInformation | Advapi32.ProtectedDaclSecurityInformation;
                    if (!Advapi32.SetKernelObjectSecurity(device, information, descriptor))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not restrict access to " + devicePath + ".");
                    }
                }
            }
            finally
            {
                Kernel32.LocalFree(descriptor);
            }
        }

        private static SafeFileHandle Open(string devicePath, uint access) =>
            Kernel32.CreateFile(devicePath, access, FileShare.ReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
    }
}
