using System;
using Ayr.App;

namespace Ayr.Hardware
{
    /// <summary>
    /// The laptop has MSI's interface registered but its firmware doesn't answer through it: newer MSI laptops (11th-gen
    /// Intel and later) use a different one that Ayr doesn't support yet.
    /// </summary>
    public sealed class UnsupportedFirmwareException : InvalidOperationException
    {
        public UnsupportedFirmwareException(Exception cause)
            : base("Ayr can't control this laptop yet: newer MSI laptops, such as 11th-gen Intel and later, use different firmware. "
                + "To help add support, run \"ayrctl probe\" (it only reads) and share the output here: " + ProjectLinks.ModelReport, cause)
        {
        }
    }
}
