using Ayr.Controllers;
using Ayr.Hardware;

namespace Ayr.Ui
{
    /// <summary>Cheap readings refreshed every couple of seconds while a window is open.</summary>
    public sealed class LiveState
    {
        public LiveState(FanMode fan, PerformanceLevel performance, TelemetrySample telemetry)
        {
            Fan = fan;
            Performance = performance;
            Telemetry = telemetry;
        }

        public FanMode Fan { get; }
        public PerformanceLevel Performance { get; }
        public TelemetrySample Telemetry { get; }
    }
}
