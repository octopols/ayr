using System;

namespace Ayr.Interop
{
    /// <summary>A service-manager or service handle, closed on dispose.</summary>
    internal sealed class ServiceControlHandle : IDisposable
    {
        public ServiceControlHandle(IntPtr value)
        {
            Value = value;
        }

        public IntPtr Value { get; private set; }

        public bool IsInvalid => Value == IntPtr.Zero;

        public void Dispose()
        {
            if (!IsInvalid)
            {
                Advapi32.CloseServiceHandle(Value);
                Value = IntPtr.Zero;
            }
        }
    }
}
