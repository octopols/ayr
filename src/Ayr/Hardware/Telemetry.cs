namespace Ayr.Hardware
{
    public sealed class Telemetry
    {
        private const double MicrosecondsPerMinute = 60000000.0;
        private const double TachTickMicroseconds = 62.5;
        private const int TachEdgesPerRevolution = 2;

        private readonly WmiBlockClient _wmi;

        public Telemetry(WmiBlockClient wmi)
        {
            _wmi = wmi;
        }

        public TelemetrySample Sample()
        {
            byte[] cpu = _wmi.ReadBlock(WmiBlock.Cpu);
            byte[] vga = _wmi.ReadBlock(WmiBlock.Vga);
            byte[] ap = _wmi.ReadBlock(WmiBlock.Ap);
            return new TelemetrySample(
                cpuTemperature: cpu[RegisterMap.TemperatureIndex],
                gpuTemperature: vga[RegisterMap.TemperatureIndex],
                cpuDuty: cpu[RegisterMap.DutyIndex],
                gpuDuty: vga[RegisterMap.DutyIndex],
                cpuFanRpm: Rpm(ap, RegisterMap.CpuFanTachIndex),
                gpuFanRpm: Rpm(ap, RegisterMap.GpuFanTachIndex));
        }

        // The tachometer value is the period between edges, not a speed (UserScenario.CalculateFanSpeed).
        private static int Rpm(byte[] ap, int lowByteIndex)
        {
            int period = (ap[lowByteIndex + 1] << 8) | ap[lowByteIndex];
            return period == 0 ? 0 : (int)(MicrosecondsPerMinute / (period * TachEdgesPerRevolution * TachTickMicroseconds));
        }
    }
}
