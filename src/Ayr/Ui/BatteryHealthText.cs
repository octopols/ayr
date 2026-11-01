using Ayr.Battery;

namespace Ayr.Ui
{
    /// <summary>What the Battery page says about the battery's health.</summary>
    public static class BatteryHealthText
    {
        private const int Good = 80;
        private const int Fair = 60;

        public static string Summary(BatteryHealth health) => Verdict(health.Percent) + ": " + health.Percent + "% of its original capacity";

        public static string Detail(BatteryHealth health)
        {
            string holds = "Holds about " + WattHours(health.FullChargeCapacityMwh) + " of the " + WattHours(health.DesignCapacityMwh)
                + " it held when new.";
            return health.CanCalibrate ? holds : holds + " It can shut down soon after unplugging; keep the laptop plugged in until it's replaced.";
        }

        public static string CalibrationUnavailable(BatteryHealth health) =>
            "Not available: the battery holds " + health.Percent + "% of its original charge, too little to run the laptop down to 5%. "
            + "Replace it first.";

        private static string Verdict(int percent)
        {
            if (percent >= Good)
            {
                return "Good";
            }
            if (percent >= Fair)
            {
                return "Fair";
            }
            return percent >= BatteryHealth.MinimumForCalibration ? "Worn" : "Needs replacing";
        }

        private static string WattHours(int milliwattHours) => (milliwattHours / 1000.0).ToString("0.0") + " Wh";
    }
}
