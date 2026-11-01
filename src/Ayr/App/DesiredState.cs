using System;
using System.Collections.Generic;
using System.Globalization;
using Ayr.Controllers;
using Ayr.Games;
using Ayr.Hardware;

namespace Ayr.App
{
    /// <summary>
    /// What the user last asked for. The supervisor uses it to know what to guard or reapply; it never overrides a change
    /// made elsewhere (for example the Cooler Boost key).
    /// </summary>
    public sealed class DesiredState
    {
        private const string FanKey = "fan";
        private const string PerformanceKey = "perf";
        private const string TurboCoreKey = "turbo_core";
        private const string TurboMemoryKey = "turbo_memory";
        private const string AppleAutoFastChargeKey = "apple_fastcharge";
        private const string AutoUpdateKey = "auto_update";
        private const string GameModeKey = "game_mode";
        private const string GameFanKey = "game_fan";
        private const string GamePerformanceKey = "game_perf";
        private const string GameOverclockKey = "game_overclock";
        private const string CustomCpuCurveKey = "custom_cpu_curve";
        private const string CustomGpuCurveKey = "custom_gpu_curve";
        private const string ChargeLimitKey = "charge_limit";
        private const string CustomChargeLimitKey = "custom_charge_limit";
        private const string FnKeyPrefix = "fn_";
        private const string CalibrationKey = "calibration";
        private const string On = "on";
        private const string Off = "off";

        private static readonly KeyValueFile StateFile = new KeyValueFile(AppPaths.StateFile);

        private readonly Dictionary<FnKey, FnKeyAction> _fnActions = new Dictionary<FnKey, FnKeyAction>();

        public FanMode Fan { get; set; } = FanMode.Auto;
        public PerformanceLevel Performance { get; set; } = PerformanceLevel.Unknown;
        public GpuOffsets TurboOffsets { get; set; } = GpuOffsets.Default;
        public FanCurves CustomCurves { get; set; } = FanCurves.Stock;
        public bool AppleAutoFastCharge { get; set; }
        public bool AutoUpdate { get; set; } = true;
        public bool GameMode { get; set; }
        public GameProfile GameProfile { get; set; } = GameProfile.Highest;

        /// <summary>True from the user starting a battery calibration until it ends or they cancel it.</summary>
        public bool CalibrationWanted { get; set; }

        /// <summary>Null until the user picks one; Ayr then leaves the battery's own setting alone.</summary>
        public int? ChargeLimitPercent { get; set; }

        /// <summary>The last custom limit, so switching from Full back to Custom restores it.</summary>
        public int CustomChargePercent { get; set; } = ChargeLimitController.DefaultCustom;

        /// <summary>Null until the user picks an action for the key; FnKeyBindings supplies the default.</summary>
        public FnKeyAction? ChosenFnAction(FnKey key) => _fnActions.TryGetValue(key, out FnKeyAction action) ? action : (FnKeyAction?)null;

        public void ChooseFnAction(FnKey key, FnKeyAction action) => _fnActions[key] = action;

        public bool NeedsSupervision => Fan == FanMode.Silent || Performance == PerformanceLevel.Turbo || AppleAutoFastCharge || GameMode || CalibrationWanted;

        public static DesiredState Load()
        {
            Dictionary<string, string> values = StateFile.Read();
            var state = new DesiredState();
            if (values.TryGetValue(FanKey, out string fan) && Enum.TryParse(fan, true, out FanMode fanMode))
            {
                state.Fan = fanMode;
            }
            if (values.TryGetValue(PerformanceKey, out string performance) && Enum.TryParse(performance, true, out PerformanceLevel level))
            {
                state.Performance = level;
            }
            if (values.TryGetValue(TurboCoreKey, out string core) && values.TryGetValue(TurboMemoryKey, out string memory))
            {
                if (GpuOffsets.TryParse(core, memory, out GpuOffsets offsets, out _))
                {
                    state.TurboOffsets = offsets;
                }
                else
                {
                    ActivityLog.Write("ignored invalid turbo offsets in state.ini: " + core + "/" + memory);
                }
            }
            state.AppleAutoFastCharge = values.TryGetValue(AppleAutoFastChargeKey, out string apple) && apple == On;
            state.AutoUpdate = !values.TryGetValue(AutoUpdateKey, out string update) || update != Off;
            state.GameMode = values.TryGetValue(GameModeKey, out string gameMode) && gameMode == On;
            state.GameProfile = LoadGameProfile(values);
            state.CalibrationWanted = values.TryGetValue(CalibrationKey, out string calibration) && calibration == On;
            if (values.TryGetValue(CustomCpuCurveKey, out string cpuText) && FanCurves.TryParse(cpuText, out byte[] cpu)
                && values.TryGetValue(CustomGpuCurveKey, out string gpuText) && FanCurves.TryParse(gpuText, out byte[] gpu))
            {
                state.CustomCurves = new FanCurves(cpu, gpu);
            }
            if (values.TryGetValue(ChargeLimitKey, out string charge)
                && int.TryParse(charge, NumberStyles.None, CultureInfo.InvariantCulture, out int percent)
                && ChargeLimitController.IsValid(percent))
            {
                state.ChargeLimitPercent = percent;
            }
            if (values.TryGetValue(CustomChargeLimitKey, out string custom)
                && int.TryParse(custom, NumberStyles.None, CultureInfo.InvariantCulture, out int customPercent)
                && ChargeLimitController.IsValid(customPercent) && customPercent != ChargeLimitController.Full)
            {
                state.CustomChargePercent = customPercent;
            }
            foreach (FnKey key in Enum.GetValues(typeof(FnKey)))
            {
                if (values.TryGetValue(FnKeyName(key), out string actionText) && Enum.TryParse(actionText, true, out FnKeyAction action))
                {
                    state.ChooseFnAction(key, action);
                }
            }
            return state;
        }

        public void Save()
        {
            var values = new Dictionary<string, string>
            {
                [FanKey] = Fan.ToString().ToLowerInvariant(),
                [PerformanceKey] = Performance.ToString().ToLowerInvariant(),
                [TurboCoreKey] = TurboOffsets.CoreMhz.ToString(CultureInfo.InvariantCulture),
                [TurboMemoryKey] = TurboOffsets.MemoryMhz.ToString(CultureInfo.InvariantCulture),
                [CustomCpuCurveKey] = FanCurves.Format(CustomCurves.Cpu),
                [CustomGpuCurveKey] = FanCurves.Format(CustomCurves.Gpu),
                [CustomChargeLimitKey] = CustomChargePercent.ToString(CultureInfo.InvariantCulture),
                [AppleAutoFastChargeKey] = AppleAutoFastCharge ? On : Off,
                [AutoUpdateKey] = AutoUpdate ? On : Off,
                [GameModeKey] = GameMode ? On : Off,
                [GameFanKey] = GameProfile.Fan.ToString().ToLowerInvariant(),
                [GamePerformanceKey] = GameProfile.Performance.ToString().ToLowerInvariant(),
                [GameOverclockKey] = GameProfile.Overclock ? On : Off,
                [CalibrationKey] = CalibrationWanted ? On : Off
            };
            if (ChargeLimitPercent.HasValue)
            {
                values[ChargeLimitKey] = ChargeLimitPercent.Value.ToString(CultureInfo.InvariantCulture);
            }
            foreach (KeyValuePair<FnKey, FnKeyAction> chosen in _fnActions)
            {
                values[FnKeyName(chosen.Key)] = chosen.Value.ToString().ToLowerInvariant();
            }
            StateFile.Write(values);
        }

        private static string FnKeyName(FnKey key) => FnKeyPrefix + key.ToString().ToLowerInvariant();

        // Each part falls back to the highest setting on its own, so one bad value does not reset the others.
        private static GameProfile LoadGameProfile(Dictionary<string, string> values)
        {
            GameProfile profile = GameProfile.Highest;
            if (values.TryGetValue(GameFanKey, out string fan) && Enum.TryParse(fan, true, out FanMode fanMode)
                && GameProfile.FanChoices.Contains(fanMode))
            {
                profile = profile.WithFan(fanMode);
            }
            if (values.TryGetValue(GamePerformanceKey, out string performance) && Enum.TryParse(performance, true, out PerformanceLevel level)
                && GameProfile.PerformanceChoices.Contains(level))
            {
                profile = profile.WithPerformance(level);
            }
            if (values.TryGetValue(GameOverclockKey, out string overclock))
            {
                profile = profile.WithOverclock(overclock != Off);
            }
            return profile;
        }

        public static void Update(Action<DesiredState> change)
        {
            DesiredState state = Load();
            change(state);
            state.Save();
        }
    }
}
