using Ayr.Battery;

namespace Ayr.Ui
{
    /// <summary>The Battery page's request for the battery's history: still running, or finished with or without it.</summary>
    public sealed class BatteryHistoryLookup
    {
        public static readonly BatteryHistoryLookup Checking = new BatteryHistoryLookup(false, null);
        public static readonly BatteryHistoryLookup Unavailable = new BatteryHistoryLookup(true, null);

        private BatteryHistoryLookup(bool finished, BatteryHistory history)
        {
            Finished = finished;
            History = history;
        }

        public bool Finished { get; }

        /// <summary>Null until found, and when Windows had none.</summary>
        public BatteryHistory History { get; }

        public static BatteryHistoryLookup Found(BatteryHistory history) => new BatteryHistoryLookup(true, history);
    }
}
