using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Ayr.Interop
{
    internal static class Advapi32
    {
        public const uint ScManagerAllAccess = 0xF003F;
        public const uint ServiceAllAccess = 0xF01FF;
        public const uint ServiceKernelDriver = 0x1;
        public const uint ServiceDemandStart = 0x3;
        public const uint ServiceDisabled = 0x4;
        public const uint ServiceErrorNormal = 0x1;
        public const uint ServiceNoChange = 0xFFFFFFFF;
        public const int ErrorServiceAlreadyRunning = 1056;
        public const int ErrorServiceMarkedForDelete = 1072;
        public const uint DaclSecurityInformation = 0x4;
        public const uint ProtectedDaclSecurityInformation = 0x80000000;
        public const uint SddlRevision1 = 1;
        public const int WmiGuidQuery = 0x1;
        public const int ErrorSuccess = 0;
        public const int ErrorInsufficientBuffer = 122;
        public const int ErrorWmiGuidNotFound = 4200;

        // Windows' own WMI data-block API: reaches a firmware block by its GUID, whether or not a class names it.
        [DllImport("advapi32.dll")]
        public static extern int WmiOpenBlock(ref Guid guid, int desiredAccess, out IntPtr dataBlock);

        [DllImport("advapi32.dll")]
        public static extern int WmiQueryAllDataW(IntPtr dataBlock, ref int bufferSize, IntPtr buffer);

        [DllImport("advapi32.dll")]
        public static extern int WmiCloseBlock(IntPtr dataBlock);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ServiceControlHandle OpenSCManager(string machine, string database, uint access);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ServiceControlHandle OpenService(ServiceControlHandle manager, string name, uint access);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ServiceControlHandle CreateService(ServiceControlHandle manager, string name, string displayName, uint access,
            uint serviceType, uint startType, uint errorControl, string binaryPath, string loadOrderGroup, IntPtr tagId,
            string dependencies, string account, string password);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeServiceConfig(ServiceControlHandle service, uint serviceType, uint startType, uint errorControl,
            string binaryPath, string loadOrderGroup, IntPtr tagId, string dependencies, string account, string password,
            string displayName);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool StartService(ServiceControlHandle service, int argumentCount, IntPtr arguments);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteService(ServiceControlHandle service);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseServiceHandle(IntPtr handle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, uint revision,
            out IntPtr securityDescriptor, IntPtr size);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetKernelObjectSecurity(SafeFileHandle handle, uint information, IntPtr securityDescriptor);
    }
}
