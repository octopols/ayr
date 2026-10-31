using System;

namespace Ayr.Hardware
{
    /// <summary>EC RAM can't be reached at all on this system: no WINIO driver, library or MSI service to go through.</summary>
    public sealed class EcUnavailableException : Exception
    {
        public EcUnavailableException(string message, Exception cause) : base(message, cause)
        {
        }
    }
}
