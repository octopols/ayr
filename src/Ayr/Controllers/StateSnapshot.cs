using System;
using System.Globalization;
using System.IO;
using Ayr.App;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    /// <summary>Everything Ayr can change, captured before a write so it can be put back exactly.</summary>
    public sealed class StateSnapshot
    {
        private StateSnapshot(FanController fans, PerformanceController performance, bool includeGpu)
        {
            TakenAt = DateTime.Now;
            CpuCurve = fans.ReadCurve(WmiBlock.Cpu);
            VgaCurve = fans.ReadCurve(WmiBlock.Vga);
            FanModeByte = fans.ReadFanModeByte();
            CoolerBoostRegister = fans.TryReadCoolerBoostRegister();
            ShiftByte = performance.ReadShiftByte();
            GpuInfo? gpu = includeGpu ? performance.TryReadGpu() : null;
            if (gpu.HasValue)
            {
                GpuCoreOffset = gpu.Value.CoreOffsetMhz;
                GpuMemoryOffset = gpu.Value.MemoryOffsetMhz;
            }
        }

        public DateTime TakenAt { get; }
        public byte[] CpuCurve { get; }
        public byte[] VgaCurve { get; }
        public byte FanModeByte { get; }
        public byte? CoolerBoostRegister { get; }
        public byte ShiftByte { get; }
        public int? GpuCoreOffset { get; }
        public int? GpuMemoryOffset { get; }

        public bool WasSilent => (FanModeByte & RegisterMap.FanModeSilentBit) != 0;

        public static StateSnapshot Capture(FanController fans, PerformanceController performance, bool includeGpu) =>
            new StateSnapshot(fans, performance, includeGpu);

        /// <summary>Cooling is restored first (curve, mode, Cooler Boost), so a restore never passes through a quieter state.</summary>
        public void Restore(FanController fans, PerformanceController performance)
        {
            fans.WriteCurves(CpuCurve, VgaCurve);
            if (fans.ReadFanModeByte() != FanModeByte)
            {
                fans.WriteFanModeByte(FanModeByte);
            }
            if (CoolerBoostRegister.HasValue)
            {
                fans.SetCoolerBoost(FanController.IsCoolerBoostOn(CoolerBoostRegister.Value));
            }
            if (performance.ReadShiftByte() != ShiftByte)
            {
                performance.WriteShiftByte(ShiftByte);
            }
            if (GpuCoreOffset.HasValue && GpuMemoryOffset.HasValue)
            {
                performance.ApplyGpuOffsets(GpuCoreOffset.Value, GpuMemoryOffset.Value);
            }
        }

        public void AppendToLog() => File.AppendAllText(AppPaths.SnapshotLog, Describe() + Environment.NewLine);

        private string Describe() => string.Format(CultureInfo.InvariantCulture,
            "taken={0:yyyy-MM-ddTHH:mm:ss} cpuCurve={1} vgaCurve={2} fanMode=0x{3:X2} shift=0x{4:X2} ec98={5} gpu={6}/{7}",
            TakenAt, string.Join(",", CpuCurve), string.Join(",", VgaCurve), FanModeByte, ShiftByte, 
            CoolerBoostRegister.HasValue ? "0x" + CoolerBoostRegister.Value.ToString("X2", CultureInfo.InvariantCulture) : "-",
            GpuCoreOffset?.ToString(CultureInfo.InvariantCulture) ?? "-", GpuMemoryOffset?.ToString(CultureInfo.InvariantCulture) ?? "-");
    }
}
