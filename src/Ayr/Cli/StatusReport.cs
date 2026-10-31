using System;
using System.IO;
using Ayr.Agent;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Hardware;

namespace Ayr.Cli
{
    public sealed class StatusReport
    {
        private readonly HardwareContext _hardware;

        public StatusReport(HardwareContext hardware)
        {
            _hardware = hardware;
        }

        public void Write(TextWriter output)
        {
            byte fanModeByte = _hardware.Fans.ReadFanModeByte();
            byte? coolerBoost = _hardware.Fans.TryReadCoolerBoostRegister();
            byte shift = _hardware.Performance.ReadShiftByte();
            TelemetrySample sample = _hardware.Telemetry.Sample();
            DesiredState desired = DesiredState.Load();

            string coolerBoostState = coolerBoost.HasValue
                ? string.Format("EC 0x98=0x{0:X2}, cooler boost {1}", coolerBoost.Value, FanController.IsCoolerBoostOn(coolerBoost.Value) ? "on" : "off")
                : "cooler boost unavailable: no EC access";
            output.WriteLine("Fan mode     : {0,-7} (EC 0xF4=0x{1:X2}, {2})", Lower(_hardware.Fans.ReadMode()), fanModeByte, coolerBoostState);
            output.WriteLine("Performance  : {0,-7} (EC 0xF2=0x{1:X2})", Lower(PerformanceController.LevelOf(shift)), shift);
            output.WriteLine("CPU          : {0} °C, fan duty {1}%, {2} RPM", sample.CpuTemperature, sample.CpuDuty, sample.CpuFanRpm);
            output.WriteLine("GPU          : {0} °C, fan duty {1}%, {2} RPM", sample.GpuTemperature, sample.GpuDuty, sample.GpuFanRpm);
            output.WriteLine("Fan curve    : CPU {0} | GPU {1}",
                string.Join(",", _hardware.Fans.ReadCurve(WmiBlock.Cpu)), string.Join(",", _hardware.Fans.ReadCurve(WmiBlock.Vga)));
            WriteGpuOffsets(output);
            output.WriteLine("Turbo OC     : {0} (limits 0-{1} / 0-{2})", desired.TurboOffsets, GpuOffsets.MaxCoreMhz, GpuOffsets.MaxMemoryMhz);
            output.WriteLine("Saved choice : fan {0}, perf {1}; supervisor {2}",
                Lower(desired.Fan), Lower(desired.Performance), Supervisor.IsRunning ? "running" : "not running");
            if (MsiSoftware.IsMsiCenterRunning)
            {
                output.WriteLine("Warning      : MSI Center is running and overrides these settings after sleep; uninstall it.");
            }
        }

        private void WriteGpuOffsets(TextWriter output)
        {
            GpuInfo gpu;
            try
            {
                gpu = _hardware.Performance.ReadGpu();
            }
            catch (Exception ex)
            {
                output.WriteLine("GPU offsets  : unavailable ({0})", ex.Message);
                return;
            }
            output.WriteLine("GPU offsets  : core {0:+0;-0;0} MHz, memory {1:+0;-0;0} MHz (max {2}/{3} MHz, P{4})",
                gpu.CoreOffsetMhz, gpu.MemoryOffsetMhz, gpu.CoreClockMax, gpu.MemoryClockMax, gpu.PState);
        }

        private static string Lower<T>(T value) where T : Enum => value.ToString().ToLowerInvariant();
    }
}
