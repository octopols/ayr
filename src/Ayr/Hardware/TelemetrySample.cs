using System;

namespace Ayr.Hardware
{
    public sealed class TelemetrySample
    {
        public TelemetrySample(int cpuTemperature, int gpuTemperature, int cpuDuty, int gpuDuty, int cpuFanRpm, int gpuFanRpm)
        {
            CpuTemperature = cpuTemperature;
            GpuTemperature = gpuTemperature;
            CpuDuty = cpuDuty;
            GpuDuty = gpuDuty;
            CpuFanRpm = cpuFanRpm;
            GpuFanRpm = gpuFanRpm;
        }

        public int CpuTemperature { get; }
        public int GpuTemperature { get; }
        public int CpuDuty { get; }
        public int GpuDuty { get; }
        public int CpuFanRpm { get; }
        public int GpuFanRpm { get; }

        public int HottestTemperature => Math.Max(CpuTemperature, GpuTemperature);
    }
}
