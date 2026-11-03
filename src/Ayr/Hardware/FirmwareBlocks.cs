using System;
using Ayr.Interop;

namespace Ayr.Hardware
{
    /// <summary>
    /// Tells older MSI firmware from newer without MSI's class definitions: Windows registers the firmware's data blocks
    /// by GUID from the ACPI tables, and newer laptops don't have MSI_System's. Only queries, never writes.
    /// </summary>
    public static class FirmwareBlocks
    {
        private static readonly Guid SystemBlock = new Guid("A1753D7B-B621-DE4A-B41A-55716A0ECE7A");
        private static readonly Lazy<FirmwareGeneration> Detected = new Lazy<FirmwareGeneration>(Detect);

        public static FirmwareGeneration Generation => Detected.Value;

        private static FirmwareGeneration Detect()
        {
            Guid block = SystemBlock;
            int opened = Advapi32.WmiOpenBlock(ref block, Advapi32.WmiGuidQuery, out IntPtr handle);
            if (opened != Advapi32.ErrorSuccess)
            {
                return Classify(opened);
            }
            try
            {
                int size = 0;
                return Classify(Advapi32.WmiQueryAllDataW(handle, ref size, IntPtr.Zero));
            }
            finally
            {
                Advapi32.WmiCloseBlock(handle);
            }
        }

        // A query with no buffer answers "buffer too small" when the block has data.
        private static FirmwareGeneration Classify(int result)
        {
            switch (result)
            {
                case Advapi32.ErrorSuccess:
                case Advapi32.ErrorInsufficientBuffer:
                    return FirmwareGeneration.Older;
                case Advapi32.ErrorWmiGuidNotFound:
                    return FirmwareGeneration.Newer;
                default:
                    return FirmwareGeneration.Unknown;
            }
        }
    }
}
