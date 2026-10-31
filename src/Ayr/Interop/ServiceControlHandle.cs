using Microsoft.Win32.SafeHandles;

namespace Ayr.Interop
{
    /// <summary>A service-manager or service handle from advapi32.</summary>
    internal sealed class ServiceControlHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private ServiceControlHandle() : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => Advapi32.CloseServiceHandle(handle);
    }
}
