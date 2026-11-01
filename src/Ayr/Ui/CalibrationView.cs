using System;
using Ayr.Battery;

namespace Ayr.Ui
{
    /// <summary>What the Battery page needs to show a calibration: whether one was asked for, its step and the battery now.</summary>
    public sealed class CalibrationView
    {
        public CalibrationView(bool wanted, CalibrationState state, BatteryReading battery)
        {
            Wanted = wanted;
            Step = state.Step;
            Battery = battery;
            LastResult = state.LastResult;
            LastCompleted = state.LastCompleted;
        }

        public bool Wanted { get; }
        public CalibrationStep Step { get; }

        /// <summary>Null when Windows can't read the battery.</summary>
        public BatteryReading Battery { get; }

        public string LastResult { get; }
        public DateTime? LastCompleted { get; }

        /// <summary>From the moment it is asked for until it has ended and put everything back.</summary>
        public bool InProgress => Wanted || Step != CalibrationStep.Idle;

        public bool ChargerUnplugged => Battery != null && !Battery.OnCharger;

        /// <summary>The steps that need the charger; during the others the laptop is meant to run on its battery.</summary>
        public bool NeedsCharger => Step == CalibrationStep.ChargingToFull || Step == CalibrationStep.Recharging;
    }
}
