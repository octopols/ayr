using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    /// <summary>
    /// Performance levels the way MSI Center applies them on v1 firmware: only the EC shift mode changes, and Turbo
    /// additionally gets the GPU offsets.
    /// </summary>
    public sealed class PerformanceController
    {
        public static readonly ReadOnlyCollection<PerformanceLevel> SelectableLevels =
            Array.AsReadOnly(new[] { PerformanceLevel.Turbo, PerformanceLevel.High, PerformanceLevel.Medium, PerformanceLevel.Low });

        private static readonly Dictionary<PerformanceLevel, byte> ShiftValues = new Dictionary<PerformanceLevel, byte>
        {
            [PerformanceLevel.Turbo] = RegisterMap.ShiftTurbo,
            [PerformanceLevel.High] = RegisterMap.ShiftHigh,
            [PerformanceLevel.Medium] = RegisterMap.ShiftMedium,
            [PerformanceLevel.Low] = RegisterMap.ShiftLow
        };

        private readonly WmiBlockClient _wmi;

        public PerformanceController(WmiBlockClient wmi)
        {
            _wmi = wmi;
        }

        public byte ReadShiftByte() => _wmi.ReadByte(WmiBlock.System, RegisterMap.ShiftModeIndex);

        public void WriteShiftByte(byte value) => _wmi.WriteByte(WmiBlock.System, RegisterMap.ShiftModeIndex, value);

        public PerformanceLevel ReadLevel() => LevelOf(ReadShiftByte());

        public static PerformanceLevel LevelOf(byte shift)
        {
            foreach (KeyValuePair<PerformanceLevel, byte> entry in ShiftValues)
            {
                if (entry.Value == shift)
                {
                    return entry.Key;
                }
            }
            return PerformanceLevel.Unknown;
        }

        public void Apply(PerformanceLevel level, GpuOffsets turboOffsets)
        {
            if (!ShiftValues.TryGetValue(level, out byte shift))
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Only turbo, high, medium and low can be applied.");
            }
            // The GPU overclock is removed before leaving Turbo and added only once Turbo is set. Without a usable GPU
            // library the level still changes; there are simply no offsets to add or remove.
            bool turbo = level == PerformanceLevel.Turbo;
            if (!turbo)
            {
                TryApplyGpuOffsets(GpuOffsets.None);
            }
            WriteShiftByte(shift);
            if (turbo)
            {
                TryApplyGpuOffsets(turboOffsets);
            }
        }

        public void ApplyGpuOffsets(GpuOffsets offsets) => ApplyGpuOffsets(offsets.CoreMhz, offsets.MemoryMhz);

        /// <summary>Returns false, and changes nothing, when GPU offsets are unavailable on this system.</summary>
        public bool TryApplyGpuOffsets(GpuOffsets offsets)
        {
            try
            {
                ApplyGpuOffsets(offsets);
                return true;
            }
            catch (GpuUnavailableException)
            {
                return false;
            }
        }

        /// <summary>Null when GPU offsets are unavailable on this system.</summary>
        public GpuInfo? TryReadGpu()
        {
            try
            {
                return ReadGpu();
            }
            catch (GpuUnavailableException)
            {
                return null;
            }
        }

        public void ApplyGpuOffsets(int coreMhz, int memoryMhz)
        {
            using (var gpu = new GpuOverclock())
            {
                gpu.Apply(coreMhz, memoryMhz);
            }
        }

        public GpuInfo ReadGpu()
        {
            using (var gpu = new GpuOverclock())
            {
                return gpu.Read();
            }
        }
    }
}
