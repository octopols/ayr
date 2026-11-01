using Ayr.Hardware;

namespace Ayr.Controllers
{
    public sealed class HardwareContext
    {
        public HardwareContext()
        {
            Wmi = new WmiBlockClient();
            Ec = new EcMemory();
            Fans = new FanController(Wmi, Ec);
            Performance = new PerformanceController(Wmi);
            Telemetry = new Telemetry(Wmi);
            ChargeLimit = new ChargeLimitController(Ec);
            KeyboardLight = new KeyboardLightController(Ec);
        }

        public WmiBlockClient Wmi { get; }
        public EcMemory Ec { get; }
        public FanController Fans { get; }
        public PerformanceController Performance { get; }
        public Telemetry Telemetry { get; }
        public ChargeLimitController ChargeLimit { get; }
        public KeyboardLightController KeyboardLight { get; }
    }
}
