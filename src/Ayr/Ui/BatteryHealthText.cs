using System;
using System.Collections.Generic;
using Ayr.Battery;
using Ayr.Hardware;
using Ayr.Ui.Controls;

namespace Ayr.Ui
{
    /// <summary>What the Battery page says about the battery's health, age and use.</summary>
    public static class BatteryHealthText
    {
        private const int Good = 80;
        private const int Fair = 60;
        private const string DayFormat = "d MMM yyyy";
        private const string MonthFormat = "MMM yyyy";

        public static string Summary(BatteryHealth health)
        {
            if (health.Percent >= Good)
            {
                return "Good";
            }
            if (health.Percent >= Fair)
            {
                return "Fair: lasts a little less than when new";
            }
            if (health.CanCalibrate)
            {
                return "Worn: lasts noticeably less than when new";
            }
            return "Needs replacing. Keep the laptop plugged in until it's replaced.";
        }

        public static IReadOnlyList<Fact> Facts(BatteryHealth health, BatteryHistoryLookup historyLookup) => new[]
        {
            new Fact("Capacity", Capacity(health, historyLookup.History)),
            new Fact("Last ran on battery", LastRanLaptop(historyLookup)),
            new Fact("Age", Age())
        };

        public static string CalibrationUnavailable(BatteryHealth health) =>
            "Not available: the battery holds " + health.Percent + "% of its original charge, too little to run the laptop down to 5%. "
            + "Replace it first.";

        // The battery estimates its capacity while it runs the laptop, so an unused battery's figure is out of date.
        private static string Capacity(BatteryHealth health, BatteryHistory history)
        {
            string capacity = WattHours(health.FullChargeCapacityMwh) + " of " + WattHours(health.DesignCapacityMwh) + " (" + health.Percent + "%)";
            return history != null && history.CapacityIsStale ? capacity + ", likely lower" : capacity;
        }

        private static string LastRanLaptop(BatteryHistoryLookup historyLookup)
        {
            if (!historyLookup.Finished)
            {
                return "Checking Windows' records…";
            }
            BatteryHistory history = historyLookup.History;
            if (history == null)
            {
                return "Windows has no record of it";
            }
            return history.LastRanLaptop.HasValue
                ? "Week of " + history.LastRanLaptop.Value.ToString(DayFormat)
                : "Not since " + history.RecordsSince.ToString(MonthFormat) + ", when Windows' records begin";
        }

        private static string Age()
        {
            DateTime? firmware = MachineIdentity.FirmwareDate;
            return firmware.HasValue
                ? "Not reported by the battery. The laptop's BIOS is from " + firmware.Value.ToString(MonthFormat)
                : "Not reported by the battery";
        }

        private static string WattHours(int milliwattHours) => (milliwattHours / 1000.0).ToString("0.0") + " Wh";
    }
}
