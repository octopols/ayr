using System;
using System.IO;
using Ayr.Controllers;
using Ayr.Hardware;

namespace Ayr.Diagnostics
{
    /// <summary>Read-only dump of the registers ayrctl uses, to cross-check the register map. Never writes.</summary>
    public sealed class ReadOnlyProbe
    {
        private static readonly byte[] EcRegisters = { RegisterMap.CoolerBoostRegister, 0xF2, 0xF3, 0xF4 };

        private readonly HardwareContext _hardware;
        private readonly TextWriter _output;

        public ReadOnlyProbe(HardwareContext hardware, TextWriter output)
        {
            _hardware = hardware;
            _output = output;
        }

        public void Run()
        {
            WriteEcRegisters();
            _output.WriteLine();
            WriteWmiBlocks();
            _output.WriteLine();
            WriteGpu();
        }

        private void WriteEcRegisters()
        {
            _output.WriteLine("=== EC RAM via KernCoreLib (WINIO driver) (read-only) ===");
            foreach (byte register in EcRegisters)
            {
                try
                {
                    byte value = _hardware.Ec.ReadByte(register);
                    _output.WriteLine("  EC 0x{0:X2} = 0x{1:X2} ({1})  bits={2}", register, value, Convert.ToString(value, 2).PadLeft(8, '0'));
                }
                catch (Exception ex)
                {
                    _output.WriteLine("  EC 0x{0:X2} : ERROR {1}", register, ex.Message);
                }
            }
        }

        private void WriteWmiBlocks()
        {
            _output.WriteLine("=== ACPI-WMI blocks (read-only) ===");
            foreach (WmiBlock block in WmiBlock.All)
            {
                try
                {
                    byte[] values = _hardware.Wmi.ReadBlock(block);
                    _output.WriteLine("  {0,-19} [{1,2}] {2}", block, values.Length, string.Join(",", values));
                }
                catch (Exception ex)
                {
                    _output.WriteLine("  {0,-19} ERROR {1}", block, ex.Message.Trim());
                }
            }
        }

        private void WriteGpu()
        {
            _output.WriteLine("=== GPU via GInf.GetGInfo (read-only) ===");
            try
            {
                GpuInfo gpu = _hardware.Performance.ReadGpu();
                _output.WriteLine("  pstate={0} coreMin={1} coreMax={2} memMin={3} memMax={4}",
                    gpu.PState, gpu.CoreClockMin, gpu.CoreClockMax, gpu.MemoryClockMin, gpu.MemoryClockMax);
                _output.WriteLine("  coreOffset={0} memOffset={1}", gpu.CoreOffsetMhz, gpu.MemoryOffsetMhz);
                _output.WriteLine("  coreClock={0} memClock={1} temp={2}", gpu.GpuClock, gpu.MemoryClock, gpu.CurrentTemperature);
            }
            catch (Exception ex)
            {
                _output.WriteLine("  ERROR " + ex.Message);
            }
        }
    }
}
