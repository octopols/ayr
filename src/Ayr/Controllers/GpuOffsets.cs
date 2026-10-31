using System;
using System.Globalization;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    /// <summary>
    /// GPU clock offsets applied in Turbo, 0..180 MHz core and 0..500 MHz memory: MSI Center's limits for the GTX 1660 Ti
    /// (UserScenario.Check_NV_OC), the verified model's GPU. Negative offsets are not offered.
    /// </summary>
    public sealed class GpuOffsets
    {
        public const int MaxCoreMhz = 180;
        public const int MaxMemoryMhz = 500;

        public static readonly GpuOffsets Default = new GpuOffsets(MaxCoreMhz, MaxMemoryMhz);
        public static readonly GpuOffsets None = new GpuOffsets(0, 0);

        public GpuOffsets(int coreMhz, int memoryMhz)
        {
            string error = Validate(coreMhz, memoryMhz);
            if (error != null)
            {
                throw new ArgumentException(error);
            }
            CoreMhz = coreMhz;
            MemoryMhz = memoryMhz;
        }

        public int CoreMhz { get; }
        public int MemoryMhz { get; }

        public static bool TryParse(string core, string memory, out GpuOffsets offsets, out string error)
        {
            offsets = null;
            if (!int.TryParse(core, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int coreMhz)
                || !int.TryParse(memory, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int memoryMhz))
            {
                error = "Offsets must be whole numbers of MHz, e.g. 150 400.";
                return false;
            }
            error = Validate(coreMhz, memoryMhz);
            if (error != null)
            {
                return false;
            }
            offsets = new GpuOffsets(coreMhz, memoryMhz);
            return true;
        }

        public bool Matches(GpuInfo gpu) => gpu.CoreOffsetMhz == CoreMhz && gpu.MemoryOffsetMhz == MemoryMhz;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "core +{0} MHz, memory +{1} MHz", CoreMhz, MemoryMhz);

        private static string Validate(int coreMhz, int memoryMhz)
        {
            if (coreMhz < 0 || coreMhz > MaxCoreMhz)
            {
                return "Core offset must be 0.." + MaxCoreMhz + " MHz.";
            }
            if (memoryMhz < 0 || memoryMhz > MaxMemoryMhz)
            {
                return "Memory offset must be 0.." + MaxMemoryMhz + " MHz.";
            }
            return null;
        }
    }
}
