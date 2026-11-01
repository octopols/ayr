using System;
using System.Collections.Generic;
using System.Globalization;
using Ayr.Controllers;

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
        private const string On = "on";
        private const string Off = "off";

        private static readonly KeyValueFile StateFile = new KeyValueFile(AppPaths.StateFile);

        public FanMode Fan { get; set; } = FanMode.Auto;
        public PerformanceLevel Performance { get; set; } = PerformanceLevel.Unknown;
        public GpuOffsets TurboOffsets { get; set; } = GpuOffsets.Default;
        public bool AppleAutoFastCharge { get; set; }
        public bool AutoUpdate { get; set; } = true;
        public bool GameMode { get; set; }

        public bool NeedsSupervision => Fan == FanMode.Silent || Performance == PerformanceLevel.Turbo || AppleAutoFastCharge || GameMode;

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
            return state;
        }

        public void Save()
        {
            StateFile.Write(new Dictionary<string, string>
            {
                [FanKey] = Fan.ToString().ToLowerInvariant(),
                [PerformanceKey] = Performance.ToString().ToLowerInvariant(),
                [TurboCoreKey] = TurboOffsets.CoreMhz.ToString(CultureInfo.InvariantCulture),
                [TurboMemoryKey] = TurboOffsets.MemoryMhz.ToString(CultureInfo.InvariantCulture),
                [AppleAutoFastChargeKey] = AppleAutoFastCharge ? On : Off,
                [AutoUpdateKey] = AutoUpdate ? On : Off,
                [GameModeKey] = GameMode ? On : Off
            });
        }

        public static void Update(Action<DesiredState> change)
        {
            DesiredState state = Load();
            change(state);
            state.Save();
        }
    }
}
