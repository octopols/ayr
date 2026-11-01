using System.Collections.Generic;

namespace Ayr.MsiRemoval
{
    /// <summary>What a scan found, and whether some places couldn't be checked, so an empty list isn't mistaken for a clean PC.</summary>
    public sealed class MsiScanResult
    {
        public MsiScanResult(IReadOnlyList<MsiComponent> components, bool incomplete)
        {
            Components = components;
            Incomplete = incomplete;
        }

        public IReadOnlyList<MsiComponent> Components { get; }
        public bool Incomplete { get; }
    }
}
