using System;
using Ayr.Battery;

namespace Ayr.Ui
{
    /// <summary>What the Battery page needs to show a calibration: whether one was asked for, its step and the charge.</summary>
    public sealed class CalibrationView
    {
        public CalibrationView(bool wanted, CalibrationState state, int? batteryPercent)
        {
            Wanted = wanted;
            Step = state.Step;
            BatteryPercent = batteryPercent;
            LastResult = state.LastResult;
            LastCompleted = state.LastCompleted;
        }

        public bool Wanted { get; }
        public CalibrationStep Step { get; }

        /// <summary>Null when Windows can't read the battery.</summary>
        public int? BatteryPercent { get; }

        public string LastResult { get; }
        public DateTime? LastCompleted { get; }

        /// <summary>From the moment it is asked for until it has ended and put everything back.</summary>
        public bool InProgress => Wanted || Step != CalibrationStep.Idle;
    }
}
