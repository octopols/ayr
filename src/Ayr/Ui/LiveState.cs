using Ayr.Controllers;
using Ayr.Hardware;

namespace Ayr.Ui
{
    /// <summary>Cheap readings refreshed every couple of seconds while a window is open.</summary>
    public sealed class LiveState
    {
        public LiveState(FanMode fan, PerformanceLevel performance, TelemetrySample telemetry, int? keyboardLight)
        {
            Fan = fan;
            Performance = performance;
            Telemetry = telemetry;
            KeyboardLight = keyboardLight;
        }

        public FanMode Fan { get; }
        public PerformanceLevel Performance { get; }
        public TelemetrySample Telemetry { get; }

        /// <summary>Read live because the Fn keys change it too; null when EC RAM can't be reached.</summary>
        public int? KeyboardLight { get; }
    }
}
