using System;

namespace Ayr.Hardware
{
    /// <summary>GPU offsets can't be used on this system: no NVIDIA GPU or driver, or no MSI GPU library to call it through.</summary>
    public sealed class GpuUnavailableException : Exception
    {
        public GpuUnavailableException(string message) : base(message)
        {
        }

        public GpuUnavailableException(string message, Exception cause) : base(message, cause)
        {
        }
    }
}
