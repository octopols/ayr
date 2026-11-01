using System;
using System.Collections.Generic;
using Ayr.App;
using Ayr.Controllers;

namespace Ayr.Battery
{
    /// <summary>
    /// MSI's battery calibration, rebuilt from MSIBatteryCalibration.exe: charge to full, run on the battery down to 5%
    /// while the charger stays plugged in, then charge again to 10%. Afterwards the battery's own idea of its capacity
    /// matches reality, so the percentage Windows shows is right again.
    ///
    /// The supervisor advances it every tick. Everything it changes is written to <see cref="CalibrationState"/> before it
    /// is changed, and <see cref="Abandon"/> puts it all back; the supervisor, the sentinel and the next start all call it,
    /// so the laptop is never left running on its battery with the charger plugged in.
    /// </summary>
    public sealed class BatteryCalibration
    {
        private const int Full = 100;
        private const int NearFull = 95;
        private const int EasedOff = 90;
        private const int EmptyTarget = 5;
        private const int RechargeTarget = 10;

        // The firmware needs a moment to change power source, and Windows a moment to notice.
        private static readonly TimeSpan SwitchGrace = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan UnpluggedGrace = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan FullSettle = TimeSpan.FromMinutes(1);

        private readonly HardwareContext _hardware;
        private readonly ActivePowerPlan _powerPlan = new ActivePowerPlan();
        private DateTime? _unpluggedSince;
        private DateTime? _batteryForcedAt;

        public BatteryCalibration(HardwareContext hardware)
        {
            _hardware = hardware;
        }

        public static bool IsRunning => CalibrationState.Load().IsRunning;

        /// <summary>From the moment the user asks for a calibration until it has ended and put everything back.</summary>
        public static bool InProgress => DesiredState.Load().CalibrationWanted || IsRunning;

        /// <summary>Moves the calibration on. Returns a message for the user when it ends, otherwise null.</summary>
        public string Advance(bool wanted)
        {
            CalibrationState state = CalibrationState.Load();
            if (!wanted)
            {
                return state.IsRunning ? End(state, "Battery calibration cancelled.", completed: false) : null;
            }
            BatteryReading battery = BatteryReading.Now();
            if (!state.IsRunning)
            {
                return Begin(state, battery);
            }
            CalibrationStep before = state.Step;
            string ended = Continue(state, battery);
            if (ended == null && state.Step != before)
            {
                state.Save();
                ActivityLog.Write("calibration: " + before + " -> " + state.Step + " at " + battery.Percent + "%");
            }
            return ended;
        }

        /// <summary>Puts everything back if a calibration was running. Returns the message for the user, or null.</summary>
        public string Abandon(string reason)
        {
            CalibrationState state = CalibrationState.Load();
            return state.IsRunning ? End(state, reason, completed: false) : null;
        }

        /// <summary>
        /// Ends a calibration a previous run of Ayr left behind. If Windows started since the laptop was put on its battery,
        /// it shut down on battery power: a battery that can't carry the laptop can't be calibrated.
        /// </summary>
        public string RecoverAfterRestart()
        {
            CalibrationState state = CalibrationState.Load();
            if (!state.IsRunning)
            {
                return null;
            }
            bool wasOnBattery = state.Step == CalibrationStep.EasingOffFull || state.Step == CalibrationStep.Emptying;
            bool windowsStartedSince = SystemClock.BootTime > state.StepSince;
            string reason = wasOnBattery && windowsStartedSince
                ? "Battery calibration failed: the laptop shut down while running on its battery, so the battery can't carry it. It needs replacing."
                : "Battery calibration stopped: Ayr or Windows restarted during it.";
            return End(state, reason, completed: false);
        }

        private string Begin(CalibrationState state, BatteryReading battery)
        {
            if (!battery.OnCharger)
            {
                return Refuse(state, "Plug in the charger, then start the battery calibration again.");
            }
            BatteryHealth health = BatteryHealth.Read(_hardware.Wmi);
            if (!health.CanCalibrate)
            {
                return Refuse(state, "Battery calibration not started: the battery holds " + health.Percent + "% of its original charge, "
                    + "too little to run the laptop down to 5%. It needs replacing.");
            }
            SaveWhatWillChange(state);
            bool easeOffFirst = battery.Percent >= NearFull && battery.Percent < Full && !battery.Charging;
            state.MoveTo(easeOffFirst ? CalibrationStep.EasingOffFull : CalibrationStep.ChargingToFull);
            state.Save();

            ApplyPowerOverrides();
            if (state.SavedChargeLimit.HasValue)
            {
                _hardware.ChargeLimit.Apply(ChargeLimitController.Full);
            }
            if (easeOffFirst)
            {
                RunOnBattery();
            }
            else
            {
                _hardware.Charger.RunOnCharger();
            }
            ActivityLog.Write("calibration started at " + battery.Percent + "%, " + state.Step);
            return null;
        }

        private string Continue(CalibrationState state, BatteryReading battery)
        {
            switch (state.Step)
            {
                case CalibrationStep.EasingOffFull:
                    return RunOnBatteryUntil(state, battery, EasedOff, CalibrationStep.ChargingToFull);
                case CalibrationStep.ChargingToFull:
                    return ChargeToFull(state, battery);
                case CalibrationStep.Emptying:
                    return RunOnBatteryUntil(state, battery, EmptyTarget, CalibrationStep.Recharging);
                case CalibrationStep.Recharging:
                    return Recharge(state, battery);
                default:
                    throw new InvalidOperationException("Unexpected calibration step " + state.Step + ".");
            }
        }

        private string RunOnBatteryUntil(CalibrationState state, BatteryReading battery, int target, CalibrationStep next)
        {
            // Sleep can reset the firmware's switch, so it is set again rather than given up on.
            if (!_hardware.Charger.IsRunningOnBattery)
            {
                RunOnBattery();
                return null;
            }
            if (battery.OnCharger)
            {
                DateTime forcedAt = _batteryForcedAt ?? state.StepSince;
                return DateTime.Now - forcedAt > SwitchGrace
                    ? End(state, "Battery calibration stopped: this laptop kept running from the charger, so the battery couldn't be emptied.", completed: false)
                    : null;
            }
            if (battery.Percent <= target)
            {
                _hardware.Charger.RunOnCharger();
                state.MoveTo(next);
            }
            return null;
        }

        private void RunOnBattery()
        {
            _hardware.Charger.RunOnBattery();
            _batteryForcedAt = DateTime.Now;
        }

        private string ChargeToFull(CalibrationState state, BatteryReading battery)
        {
            if (!battery.OnCharger)
            {
                return EndIfUnpluggedTooLong(state);
            }
            _unpluggedSince = null;
            bool full = battery.Percent >= Full || (battery.Idle && state.TimeInStep > FullSettle);
            if (full)
            {
                RunOnBattery();
                state.MoveTo(CalibrationStep.Emptying);
            }
            return null;
        }

        private string Recharge(CalibrationState state, BatteryReading battery)
        {
            if (!battery.OnCharger)
            {
                return EndIfUnpluggedTooLong(state);
            }
            _unpluggedSince = null;
            return battery.Percent >= RechargeTarget ? End(state, "Battery calibrated. Windows now shows the right percentage.", completed: true) : null;
        }

        private string EndIfUnpluggedTooLong(CalibrationState state)
        {
            _unpluggedSince = _unpluggedSince ?? DateTime.Now;
            return DateTime.Now - _unpluggedSince.Value > UnpluggedGrace
                ? End(state, "Battery calibration stopped: the charger was unplugged.", completed: false)
                : null;
        }

        private void SaveWhatWillChange(CalibrationState state)
        {
            state.SavedChargeLimit = _hardware.ChargeLimit.IsSupported ? _hardware.ChargeLimit.ReadPercent() : (int?)null;
            state.SavedPower.Clear();
            foreach (PowerOverride powerOverride in CalibrationPowerSettings.All)
            {
                state.SavedPower[powerOverride.Setting.Key] = _powerPlan.Read(powerOverride.Setting);
            }
        }

        private void ApplyPowerOverrides()
        {
            foreach (PowerOverride powerOverride in CalibrationPowerSettings.All)
            {
                _powerPlan.Write(powerOverride.Setting, powerOverride.AppliedTo(_powerPlan.Read(powerOverride.Setting)));
            }
            _powerPlan.Activate();
        }

        // Every part is put back even if another fails; the state stays "running" until all of it succeeds, so the
        // next tick, the sentinel or the next start tries again.
        private string End(CalibrationState state, string message, bool completed)
        {
            var failures = new List<string>();
            TryRestore(failures, "charger", () => _hardware.Charger.RunOnCharger());
            if (state.SavedChargeLimit.HasValue)
            {
                TryRestore(failures, "charge limit", () => _hardware.ChargeLimit.Apply(state.SavedChargeLimit.Value));
            }
            TryRestore(failures, "power plan", () => RestorePower(state.SavedPower));
            if (failures.Count > 0)
            {
                ActivityLog.Write("calibration: couldn't restore " + string.Join(", ", failures) + "; will retry");
                return null;
            }
            state.MoveTo(CalibrationStep.Idle);
            state.SavedChargeLimit = null;
            state.SavedPower.Clear();
            state.LastResult = message;
            if (completed)
            {
                state.LastCompleted = DateTime.Now;
            }
            state.Save();
            ClearRequest();
            _unpluggedSince = null;
            _batteryForcedAt = null;
            ActivityLog.Write("calibration ended: " + message);
            return message;
        }

        private void RestorePower(IReadOnlyDictionary<string, PowerValues> saved)
        {
            foreach (PowerOverride powerOverride in CalibrationPowerSettings.All)
            {
                if (saved.TryGetValue(powerOverride.Setting.Key, out PowerValues values))
                {
                    _powerPlan.Write(powerOverride.Setting, values);
                }
            }
            _powerPlan.Activate();
        }

        private static void TryRestore(List<string> failures, string part, Action restore)
        {
            try
            {
                restore();
            }
            catch (Exception ex)
            {
                ActivityLog.Write("calibration: restoring the " + part + " failed: " + ex.Message);
                failures.Add(part);
            }
        }

        private static string Refuse(CalibrationState state, string reason)
        {
            state.LastResult = reason;
            state.Save();
            ClearRequest();
            ActivityLog.Write("calibration not started: " + reason);
            return reason;
        }

        private static void ClearRequest() => DesiredState.Update(desired => desired.CalibrationWanted = false);
    }
}
