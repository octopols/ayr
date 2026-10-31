using Ayr.App;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    public sealed class HardwareContext
    {
        public HardwareContext()
        {
            Wmi = new WmiBlockClient();
            // Unelevated processes cannot load the WINIO library, so they go through MSI Foundation Service's pipe.
            Ec = Elevation.IsElevated ? (EcMemory)new KernCoreEcMemory() : new FoundationServiceEcMemory();
            Fans = new FanController(Wmi, Ec);
            Performance = new PerformanceController(Wmi);
            Telemetry = new Telemetry(Wmi);
        }

        public WmiBlockClient Wmi { get; }
        public EcMemory Ec { get; }
        public FanController Fans { get; }
        public PerformanceController Performance { get; }
        public Telemetry Telemetry { get; }
    }
}
