using System;
using Ayr.Battery;

namespace Ayr.Ui
{
    /// <summary>What the Battery page says about calibration before, during and after a run.</summary>
    public static class CalibrationText
    {
        public const string Explanation =
            "Empties and refills the battery once so its percentage is right again. It takes a few hours with the charger "
            + "plugged in. MSI suggests it every three months.";

        public const string PlugInCharger = "Plug in the charger to calibrate.";

        public const string WhileRunning =
            "Keep the charger plugged in and Ayr running. You can use the laptop meanwhile. While the battery runs down, "
            + "Windows shows it on battery even though the charger is plugged in.";

        private const string ConfirmationStart =
            "Takes a few hours. Keep the charger plugged in and Ayr running: exiting Ayr stops it. You can keep using the laptop. "
            + "Until it ends the laptop won't sleep, turn off its screen or sleep when the lid closes, so don't put it in a bag.";

        private const string ChargeLimitHeld = " Your charge limit is held at Full.";

        private const string UnpluggedMidway = "Charger unplugged. Plug it back in within a minute or the calibration stops";

        private static readonly TimeSpan SuggestedInterval = TimeSpan.FromDays(91);

        public static string Confirmation(bool hasChargeLimit) => hasChargeLimit ? ConfirmationStart + ChargeLimitHeld : ConfirmationStart;

        public static string Summary(CalibrationView calibration)
        {
            if (!calibration.InProgress)
            {
                return calibration.LastCompleted.HasValue
                    ? "Last done " + calibration.LastCompleted.Value.ToString("d MMM yyyy") + ". Next suggested " + (calibration.LastCompleted.Value + SuggestedInterval).ToString("d MMM") + "."
                    : "Not done yet";
            }
            if (calibration.NeedsCharger && calibration.ChargerUnplugged)
            {
                return UnpluggedMidway;
            }
            return Progress(calibration.Step) + Charge(calibration.Battery);
        }

        public static string LastResultAndExplanation(CalibrationView calibration) =>
            calibration.LastResult == null ? Explanation : calibration.LastResult + Environment.NewLine + Explanation;

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

        private static string Charge(BatteryReading battery) => battery == null ? "" : " (now " + battery.Percent + "%)";
    }
}
