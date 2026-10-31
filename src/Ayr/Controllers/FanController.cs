using System;
using System.Collections.ObjectModel;
using System.Linq;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    /// <summary>
    /// Fan modes the way MSI Center sets them on v1 firmware: Auto is the stock curve with no mode bits, Max is the
    /// advanced bit with every curve point at 100%, Silent is the silent bit, and Boost is the EC's Cooler Boost bit.
    /// </summary>
    public sealed class FanController
    {
        public static readonly ReadOnlyCollection<FanMode> SelectableModes =
            Array.AsReadOnly(new[] { FanMode.Auto, FanMode.Max, FanMode.Silent, FanMode.Boost });

        private const byte FullDuty = 100;
        private static readonly byte[] FullCurve = Enumerable.Repeat(FullDuty, RegisterMap.CurvePointCount).ToArray();

        private readonly WmiBlockClient _wmi;
        private readonly EcMemory _ec;

        public FanController(WmiBlockClient wmi, EcMemory ec)
        {
            _wmi = wmi;
            _ec = ec;
        }

        public FanMode ReadMode()
        {
            byte? coolerBoost = TryReadCoolerBoostRegister();
            if (coolerBoost.HasValue && IsCoolerBoostOn(coolerBoost.Value))
            {
                return FanMode.Boost;
            }
            byte mode = ReadFanModeByte();
            if ((mode & RegisterMap.FanModeSilentBit) != 0)
            {
                return FanMode.Silent;
            }
            if ((mode & RegisterMap.FanModeAdvancedBit) != 0 && CurvesEqual(FullCurve, FullCurve))
            {
                return FanMode.Max;
            }
            if ((mode & RegisterMap.FanModeBits) == 0 && CurvesEqual(RegisterMap.StockCpuCurve, RegisterMap.StockVgaCurve))
            {
                return FanMode.Auto;
            }
            return FanMode.Custom;
        }

        public void Apply(FanMode mode)
        {
            switch (mode)
            {
                case FanMode.Auto:
                    WriteCurves(RegisterMap.StockCpuCurve, RegisterMap.StockVgaCurve);
                    WriteFanModeBits(0);
                    SetCoolerBoost(false);
                    break;
                case FanMode.Max:
                    // Cooling goes up before Cooler Boost goes off, so there is no dip in between.
                    WriteCurves(FullCurve, FullCurve);
                    WriteFanModeBits(RegisterMap.FanModeAdvancedBit);
                    SetCoolerBoost(false);
                    break;
                case FanMode.Silent:
                    WriteCurves(RegisterMap.StockCpuCurve, RegisterMap.StockVgaCurve);
                    WriteFanModeBits(RegisterMap.FanModeSilentBit);
                    SetCoolerBoost(false);
                    break;
                case FanMode.Boost:
                    SetCoolerBoost(true);
                    WriteCurves(RegisterMap.StockCpuCurve, RegisterMap.StockVgaCurve);
                    WriteFanModeBits(0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Only auto, max, silent and boost can be applied.");
            }
        }

        public byte ReadFanModeByte() => _wmi.ReadByte(WmiBlock.System, RegisterMap.FanModeIndex);

        public void WriteFanModeByte(byte value) => _wmi.WriteByte(WmiBlock.System, RegisterMap.FanModeIndex, value);

        public byte ReadCoolerBoostRegister() => _ec.ReadByte(RegisterMap.CoolerBoostRegister);

        /// <summary>Null when EC RAM can't be reached on this system; every other fan control still works through WMI.</summary>
        public byte? TryReadCoolerBoostRegister()
        {
            try
            {
                return ReadCoolerBoostRegister();
            }
            catch (EcUnavailableException)
            {
                return null;
            }
        }

        public static bool IsCoolerBoostOn(byte coolerBoostRegister) => (coolerBoostRegister & RegisterMap.CoolerBoostBit) != 0;

        public void SetCoolerBoost(bool on)
        {
            if (on)
            {
                SetCoolerBoostBit(ReadCoolerBoostRegister(), on: true);
                return;
            }
            byte? current = TryReadCoolerBoostRegister();
            if (current.HasValue)
            {
                SetCoolerBoostBit(current.Value, on: false);
            }
        }

        private void SetCoolerBoostBit(byte current, bool on)
        {
            byte wanted = on ? (byte)(current | RegisterMap.CoolerBoostBit) : (byte)(current & ~RegisterMap.CoolerBoostBit);
            if (wanted != current)
            {
                _ec.WriteByte(RegisterMap.CoolerBoostRegister, wanted);
            }
        }

        public byte[] ReadCurve(WmiBlock block) =>
            _wmi.ReadBlock(block).Skip(RegisterMap.CurveFirstIndex).Take(RegisterMap.CurvePointCount).ToArray();

        public void WriteCurves(byte[] cpu, byte[] vga)
        {
            WriteCurve(WmiBlock.Cpu, cpu);
            WriteCurve(WmiBlock.Vga, vga);
        }

        private void WriteCurve(WmiBlock block, byte[] curve)
        {
            byte[] current = ReadCurve(block);
            for (int point = 0; point < curve.Length; point++)
            {
                if (current[point] != curve[point])
                {
                    _wmi.WriteByte(block, RegisterMap.CurveFirstIndex + point, curve[point]);
                }
            }
        }

        private void WriteFanModeBits(byte modeBits)
        {
            byte current = ReadFanModeByte();
            byte wanted = (byte)((current & ~RegisterMap.FanModeBits) | modeBits);
            if (wanted != current)
            {
                WriteFanModeByte(wanted);
            }
        }

        private bool CurvesEqual(byte[] cpu, byte[] vga) =>
            ReadCurve(WmiBlock.Cpu).SequenceEqual(cpu) && ReadCurve(WmiBlock.Vga).SequenceEqual(vga);
    }
}
