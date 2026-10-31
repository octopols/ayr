using System.Runtime.InteropServices;

namespace Ayr.Hardware
{
    /// <summary>GInf.GetGInfo's output, field for field.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GpuInfo
    {
        public int PState;
        public uint CoreClockMin;
        public uint CoreClockMax;
        public uint MemoryClockMin;
        public uint MemoryClockMax;
        public uint GpuOverClocks;
        public uint MemoryOverClocks;
        public uint GpuClock;
        public uint MemoryClock;
        public uint CurrentTemperature;

        // GInf reports signed offsets in unsigned fields.
        public int CoreOffsetMhz => unchecked((int)GpuOverClocks);

        public int MemoryOffsetMhz => unchecked((int)MemoryOverClocks);
    }
}
