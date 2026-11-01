using System;
using System.Collections.Generic;
using System.Globalization;
using Ayr.App;

namespace Ayr.Battery
{
    /// <summary>
    /// A calibration's progress and everything it changed, kept in a file so that a crash, a restart or another Ayr
    /// process can always put the charger, the charge limit and the power plan back the way they were.
    /// </summary>
    public sealed class CalibrationState
    {
        private const string StepKey = "step";
        private const string StepSinceKey = "step_since";
        private const string SavedChargeLimitKey = "saved_charge_limit";
        private const string PowerPrefix = "power_";
        private const string PluggedInSuffix = "_ac";
        private const string OnBatterySuffix = "_dc";
        private const string LastResultKey = "last_result";
        private const string LastCompletedKey = "last_completed";

        private static readonly KeyValueFile StateFile = new KeyValueFile(AppPaths.CalibrationFile);

        public CalibrationStep Step { get; private set; } = CalibrationStep.Idle;
        public DateTime StepSince { get; private set; } = DateTime.Now;
        public TimeSpan TimeInStep => DateTime.Now - StepSince;

        /// <summary>The user's charge limit, held at full during the run; null when the laptop has none Ayr controls.</summary>
        public int? SavedChargeLimit { get; set; }

        /// <summary>The power plan values the run replaced, by setting key.</summary>
        public Dictionary<string, PowerValues> SavedPower { get; } = new Dictionary<string, PowerValues>();

        public string LastResult { get; set; }
        public DateTime? LastCompleted { get; set; }

        /// <summary>True from the moment the run changed anything until everything is put back.</summary>
        public bool IsRunning => Step != CalibrationStep.Idle;

        public void MoveTo(CalibrationStep step)
        {
            Step = step;
            StepSince = DateTime.Now;
        }

        public static CalibrationState Load()
        {
            Dictionary<string, string> values = StateFile.Read();
            var state = new CalibrationState();
            if (values.TryGetValue(StepKey, out string step) && Enum.TryParse(step, true, out CalibrationStep parsedStep))
            {
                state.Step = parsedStep;
            }
            if (values.TryGetValue(StepSinceKey, out string since) && TryParseDate(since, out DateTime stepSince))
            {
                state.StepSince = stepSince;
            }
            if (values.TryGetValue(SavedChargeLimitKey, out string limit) && int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out int percent))
            {
                state.SavedChargeLimit = percent;
            }
            foreach (PowerOverride powerOverride in CalibrationPowerSettings.All)
            {
                string key = powerOverride.Setting.Key;
                if (values.TryGetValue(PowerPrefix + key + PluggedInSuffix, out string pluggedIn) && uint.TryParse(pluggedIn, out uint pluggedInValue)
                    && values.TryGetValue(PowerPrefix + key + OnBatterySuffix, out string onBattery) && uint.TryParse(onBattery, out uint onBatteryValue))
                {
                    state.SavedPower[key] = new PowerValues(pluggedInValue, onBatteryValue);
                }
            }
            state.LastResult = values.TryGetValue(LastResultKey, out string result) ? result : null;
            if (values.TryGetValue(LastCompletedKey, out string completed) && TryParseDate(completed, out DateTime lastCompleted))
            {
                state.LastCompleted = lastCompleted;
            }
            return state;
        }

        public void Save()
        {
            var values = new Dictionary<string, string>
            {
                [StepKey] = Step.ToString(),
                [StepSinceKey] = StepSince.ToString("o", CultureInfo.InvariantCulture)
            };
            if (SavedChargeLimit.HasValue)
            {
                values[SavedChargeLimitKey] = SavedChargeLimit.Value.ToString(CultureInfo.InvariantCulture);
            }
            foreach (KeyValuePair<string, PowerValues> saved in SavedPower)
            {
                values[PowerPrefix + saved.Key + PluggedInSuffix] = saved.Value.PluggedIn.ToString(CultureInfo.InvariantCulture);
                values[PowerPrefix + saved.Key + OnBatterySuffix] = saved.Value.OnBattery.ToString(CultureInfo.InvariantCulture);
            }
            if (LastResult != null)
            {
                values[LastResultKey] = LastResult;
            }
            if (LastCompleted.HasValue)
            {
                values[LastCompletedKey] = LastCompleted.Value.ToString("o", CultureInfo.InvariantCulture);
            }
            StateFile.Write(values);
        }

        private static bool TryParseDate(string text, out DateTime date) =>
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date);
    }
}
