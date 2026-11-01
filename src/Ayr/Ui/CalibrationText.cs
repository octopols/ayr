using System;
using Ayr.Battery;

namespace Ayr.Ui
{
    /// <summary>What the Battery page says about calibration before, during and after a run.</summary>
    public static class CalibrationText
    {
        public const string Explanation =
            "Empties and refills the battery once so its percentage is right again. It takes a few hours with the charger "
            + "plugged in, and the laptop stays awake meanwhile. MSI suggests it every three months.";

        public const string Confirmation =
            "Keep the charger plugged in. The laptop charges to 100%, runs on its battery down to 5% and charges again. "
            + "Your charge limit is held at Full until it ends.";

        private static readonly TimeSpan SuggestedInterval = TimeSpan.FromDays(91);

        public static string Summary(CalibrationView calibration)
        {
            if (!calibration.InProgress)
            {
                return calibration.LastCompleted.HasValue
                    ? "Last done " + calibration.LastCompleted.Value.ToString("d MMM yyyy") + ". Next suggested " + (calibration.LastCompleted.Value + SuggestedInterval).ToString("d MMM") + "."
                    : "Not done yet";
            }
            return Progress(calibration.Step) + Charge(calibration.BatteryPercent);
        }

        /// <summary>What happened last time, or null before the first run and while one is running.</summary>
        public static string LastResult(CalibrationView calibration) => calibration.InProgress ? null : calibration.LastResult;

        private static string Progress(CalibrationStep step)
        {
            switch (step)
            {
                case CalibrationStep.Idle:
                    return "Starting";
                case CalibrationStep.EasingOffFull:
                    return "Getting ready: running on battery down to 90%";
                case CalibrationStep.ChargingToFull:
                    return "Step 1 of 3: charging to 100%";
                case CalibrationStep.Emptying:
                    return "Step 2 of 3: running on battery down to 5%";
                case CalibrationStep.Recharging:
                    return "Step 3 of 3: charging back to 10%";
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown calibration step.");
            }
        }

        private static string Charge(int? percent) => percent.HasValue ? " (now " + percent.Value + "%)" : "";
    }
}
