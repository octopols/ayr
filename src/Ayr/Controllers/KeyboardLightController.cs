using System;
using System.Collections.ObjectModel;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    /// <summary>The keyboard backlight level, from 0 (off) to 3. The laptop's Fn keys change it too.</summary>
    public sealed class KeyboardLightController
    {
        /// <summary>One name per level, from 0 up.</summary>
        public static readonly ReadOnlyCollection<string> LevelNames = Array.AsReadOnly(new[] { "Off", "Low", "Medium", "High" });

        private readonly EcMemory _ec;
        private readonly Lazy<bool> _supported;

        public KeyboardLightController(EcMemory ec)
        {
            _ec = ec;
            _supported = new Lazy<bool>(() => EcFirmware.Read(ec).HasChargeLimitAndKeyboardLight);
        }

        /// <summary>Whether this laptop's firmware is known to keep the backlight level where Ayr writes it.</summary>
        public bool IsSupported => _supported.Value;

        public int ReadLevel()
        {
            int level = _ec.ReadByte(RegisterMap.KeyboardLightRegister) - RegisterMap.KeyboardLightBase;
            return Math.Max(0, Math.Min(RegisterMap.KeyboardLightMaxLevel, level));
        }

        public void Apply(int level)
        {
            if (!IsSupported)
            {
                throw new NotSupportedException("Ayr doesn't know where this laptop's firmware keeps the keyboard light.");
            }
            if (level < 0 || level > RegisterMap.KeyboardLightMaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "The keyboard light level is 0 to " + RegisterMap.KeyboardLightMaxLevel + ".");
            }
            _ec.WriteByte(RegisterMap.KeyboardLightRegister, (byte)(RegisterMap.KeyboardLightBase + level));
        }
    }
}
