namespace Ayr.Hardware
{
    /// <summary>
    /// Register map for the MS-16W1 (GF65 Thin 10SDR), EC firmware 16W1EMS2; evidence for every entry is in FINDINGS.md.
    /// MSI_System[i] mirrors EC RAM 0xEB + i.
    /// </summary>
    public static class RegisterMap
    {
        // Performance ("shift") mode: MSI_System[7] = EC 0xF2.
        public const int ShiftModeIndex = 7;
        public const byte ShiftTurbo = 0xC4;
        public const byte ShiftHigh = 0xC0;
        public const byte ShiftMedium = 0xC1;
        public const byte ShiftLow = 0xC2;

        // Fan mode: MSI_System[9] = EC 0xF4.
        public const int FanModeIndex = 9;
        public const byte FanModeAdvancedBit = 0x80;
        public const byte FanModeBasicBit = 0x40;
        public const byte FanModeSilentBit = 0x10;
        public const byte FanModeBits = FanModeAdvancedBit | FanModeBasicBit | FanModeSilentBit;

        // Cooler Boost is not mirrored in any WMI block, so it is reached through EC RAM directly.
        public const byte CoolerBoostRegister = 0x98;
        public const byte CoolerBoostBit = 0x80;

        public const int TemperatureIndex = 1;
        public const int DutyIndex = 2;
        public const int CurveFirstIndex = 11;
        public const int CurvePointCount = 7;

        // Fan tachometers in MSI_AP: CPU fan at [2..3], GPU fan at [4..5], low byte first.
        public const int CpuFanTachIndex = 2;
        public const int GpuFanTachIndex = 4;

        // Firmware handshake MSI Foundation Service performed at every start.
        public const int HandshakeIndex = 0;
        public const byte HandshakeValue = 1;

        // Stock duty points, measured; Dragon Center's "Default_Fan" registry value agrees.
        public static readonly byte[] StockCpuCurve = { 38, 43, 48, 54, 60, 70, 85 };
        public static readonly byte[] StockVgaCurve = { 0, 43, 48, 54, 60, 70, 85 };
    }
}
