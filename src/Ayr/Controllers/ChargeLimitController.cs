using System;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    /// <summary>
    /// Where the battery stops charging: full, or a custom percentage. Keeping a mostly plugged-in battery below full
    /// slows its wear.
    /// </summary>
    public sealed class ChargeLimitController
    {
        public const int Full = 100;
        public const int MinimumCustom = 50;
        public const int MaximumCustom = 95;
        public const int DefaultCustom = 80;

        private readonly EcMemory _ec;
        private readonly Lazy<bool> _supported;

        public ChargeLimitController(EcMemory ec)
        {
            _ec = ec;
            _supported = new Lazy<bool>(() => EcFirmware.Read(ec).HasChargeLimitAndKeyboardLight);
        }

        /// <summary>Whether this laptop's firmware is known to keep the charge limit where Ayr writes it.</summary>
        public bool IsSupported => _supported.Value;

        public static bool IsValid(int percent) => percent == Full || (percent >= MinimumCustom && percent <= MaximumCustom);

        /// <summary>The percentage charging stops at; 100 when no limit is set.</summary>
        public int ReadPercent()
        {
            byte register = _ec.ReadByte(RegisterMap.ChargeLimitRegister);
            bool enabled = (register & RegisterMap.ChargeLimitEnableBit) != 0;
            int percent = register & ~RegisterMap.ChargeLimitEnableBit;
            return enabled && percent > 0 && percent < Full ? percent : Full;
        }

        public void Apply(int percent)
        {
            if (!IsSupported)
            {
                throw new NotSupportedException("Ayr doesn't know where this laptop's firmware keeps the charge limit.");
            }
            if (!IsValid(percent))
            {
                throw new ArgumentOutOfRangeException(nameof(percent), percent,
                    "The charge limit is full (100%) or " + MinimumCustom + " to " + MaximumCustom + "%.");
            }
            byte wanted = (byte)(RegisterMap.ChargeLimitEnableBit | percent);
            if (_ec.ReadByte(RegisterMap.ChargeLimitRegister) != wanted)
            {
                _ec.WriteByte(RegisterMap.ChargeLimitRegister, wanted);
            }
        }
    }
}
