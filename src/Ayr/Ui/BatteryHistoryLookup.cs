using Ayr.Battery;

namespace Ayr.Ui
{
    /// <summary>The Battery page's request for the battery's history: still running, failed, or finished with what Windows had.</summary>
    public sealed class BatteryHistoryLookup
    {
        public static readonly BatteryHistoryLookup Checking = new BatteryHistoryLookup(false, false, null);
        public static readonly BatteryHistoryLookup Failed = new BatteryHistoryLookup(true, true, null);

        private BatteryHistoryLookup(bool finished, bool failed, BatteryHistory history)
        {
            Finished = finished;
            HasFailed = failed;
            History = history;
        }

        public bool Finished { get; }
        public bool HasFailed { get; }

        /// <summary>Null until found, when reading failed, and when Windows had no records.</summary>
        public BatteryHistory History { get; }

        /// <param name="history">Null when Windows had no records.</param>
        public static BatteryHistoryLookup Found(BatteryHistory history) => new BatteryHistoryLookup(true, false, history);
    }
}
