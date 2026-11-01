using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    /// <summary>
    /// A custom fan curve for both fans: one duty percentage per temperature step. Every curve is kept safe by
    /// construction: duty never falls as temperature rises, and the hottest steps never run slower than Auto's.
    /// </summary>
    public sealed class FanCurves
    {
        public const int MaxDuty = 100;

        // Steps from here up keep at least Auto's duty, so a quiet curve can't hold back cooling when it matters.
        private const int FirstProtectedPoint = RegisterMap.CurvePointCount - 2;

        public static readonly FanCurves Stock = new FanCurves(RegisterMap.StockCpuCurve, RegisterMap.StockVgaCurve);

        private readonly byte[] _cpu;
        private readonly byte[] _gpu;

        public FanCurves(IReadOnlyList<byte> cpu, IReadOnlyList<byte> gpu)
        {
            _cpu = Safe(cpu, RegisterMap.StockCpuCurve);
            _gpu = Safe(gpu, RegisterMap.StockVgaCurve);
        }

        public IReadOnlyList<byte> Cpu => _cpu;
        public IReadOnlyList<byte> Gpu => _gpu;

        /// <summary>The lowest duty a point may have; the editor uses it to stop a drag.</summary>
        public static byte MinimumDuty(FanKind fan, int point) =>
            point >= FirstProtectedPoint ? StockCurve(fan)[point] : (byte)0;

        public FanCurves With(FanKind fan, int point, byte duty)
        {
            byte[] changed = (fan == FanKind.Cpu ? _cpu : _gpu).ToArray();
            changed[point] = duty;
            // Later points rise with an earlier one that passes them, and earlier points drop with a later one.
            for (int later = point + 1; later < changed.Length; later++)
            {
                changed[later] = Math.Max(changed[later], duty);
            }
            for (int earlier = point - 1; earlier >= 0; earlier--)
            {
                changed[earlier] = Math.Min(changed[earlier], duty);
            }
            return fan == FanKind.Cpu ? new FanCurves(changed, _gpu) : new FanCurves(_cpu, changed);
        }

        public IReadOnlyList<byte> For(FanKind fan) => fan == FanKind.Cpu ? Cpu : Gpu;

        public bool SameAs(FanCurves other) => _cpu.SequenceEqual(other._cpu) && _gpu.SequenceEqual(other._gpu);

        public static string Format(IReadOnlyList<byte> curve) =>
            string.Join(",", curve.Select(duty => duty.ToString(CultureInfo.InvariantCulture)));

        /// <summary>Parses "38,43,48,54,60,70,85"; false when there aren't seven whole numbers from 0 to 100.</summary>
        public static bool TryParse(string text, out byte[] curve)
        {
            string[] parts = (text ?? "").Split(',');
            curve = new byte[RegisterMap.CurvePointCount];
            if (parts.Length != curve.Length)
            {
                return false;
            }
            for (int point = 0; point < parts.Length; point++)
            {
                if (!byte.TryParse(parts[point].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out curve[point]) || curve[point] > MaxDuty)
                {
                    return false;
                }
            }
            return true;
        }

        private static IReadOnlyList<byte> StockCurve(FanKind fan) => fan == FanKind.Cpu ? RegisterMap.StockCpuCurve : RegisterMap.StockVgaCurve;

        private static byte[] Safe(IReadOnlyList<byte> curve, IReadOnlyList<byte> stock)
        {
            if (curve.Count != RegisterMap.CurvePointCount)
            {
                throw new ArgumentException("A fan curve has " + RegisterMap.CurvePointCount + " points.", nameof(curve));
            }
            var safe = new byte[curve.Count];
            byte floor = 0;
            for (int point = 0; point < curve.Count; point++)
            {
                byte minimum = point >= FirstProtectedPoint ? stock[point] : (byte)0;
                floor = Math.Max(floor, Math.Max(minimum, Math.Min(curve[point], (byte)MaxDuty)));
                safe[point] = floor;
            }
            return safe;
        }
    }
}
