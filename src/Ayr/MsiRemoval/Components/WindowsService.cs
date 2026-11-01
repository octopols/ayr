using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Ayr.Interop;
using Microsoft.Win32;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// An MSI service or kernel driver. Services are stopped and deleted. Drivers are never stopped, because stopping one
    /// of MSI's has crashed Windows: they are disabled and marked for deletion, and Windows removes them, with their files,
    /// at the next restart.
    /// </summary>
    public sealed class WindowsService : MsiComponent
    {
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);

        private readonly string _serviceName;
        private readonly string _binaryPath;

        public WindowsService(MsiComponentKind kind, string serviceName, string displayName, string binaryPath)
            : base(kind, displayName, binaryPath)
        {
            _serviceName = serviceName;
            _binaryPath = binaryPath;
        }

        private static string RegistryKeyOf(string serviceName) => @"SYSTEM\CurrentControlSet\Services\" + serviceName;

        public override RemovalCompletion Remove(RegistryBackup backup)
        {
            backup.Export(RegistryHive.LocalMachine, RegistryView.Registry64, RegistryKeyOf(_serviceName));
            bool isDriver = Kind == MsiComponentKind.Driver;
            if (!isDriver)
            {
                StopIfRunning();
            }
            DeleteRegistration(disableFirst: isDriver);
            bool fileDeferred = File.Exists(_binaryPath) && FileEraser.DeleteFile(_binaryPath);
            return isDriver || fileDeferred ? RemovalCompletion.AtRestart : RemovalCompletion.Now;
        }

        private void StopIfRunning()
        {
            using (var service = new ServiceController(_serviceName))
            {
                if (service.Status != ServiceControllerStatus.Stopped)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, StopTimeout);
                }
            }
        }

        private void DeleteRegistration(bool disableFirst)
        {
            using (ServiceControlHandle manager = Advapi32.OpenSCManager(null, null, Advapi32.ScManagerAllAccess))
            using (ServiceControlHandle service = Advapi32.OpenService(manager, _serviceName, Advapi32.ServiceAllAccess))
            {
                if (service.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the " + _serviceName + " service.");
                }
                if (disableFirst && !Advapi32.ChangeServiceConfig(service, Advapi32.ServiceNoChange, Advapi32.ServiceDisabled,
                    Advapi32.ServiceNoChange, null, null, IntPtr.Zero, null, null, null, null))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not disable the " + _serviceName + " driver.");
                }
                if (!Advapi32.DeleteService(service))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != Advapi32.ErrorServiceMarkedForDelete)
                    {
                        throw new Win32Exception(error, "Could not delete the " + _serviceName + " service.");
                    }
                }
            }
        }
    }
}
