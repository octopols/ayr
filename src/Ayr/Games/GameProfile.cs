using System;
using System.Collections.ObjectModel;
using Ayr.Controllers;

namespace Ayr.Games
{
    /// <summary>
    /// What game mode switches to while a game runs. The default is everything at its highest, which is why people turn
    /// game mode on; each part can be dialled back for a quieter or cooler session.
    /// </summary>
    public sealed class GameProfile : IEquatable<GameProfile>
    {
        /// <summary>Silent is left out: it hands the fans back to Auto at 90°C, which a game reaches quickly.</summary>
        public static readonly ReadOnlyCollection<FanMode> FanChoices =
            Array.AsReadOnly(new[] { FanMode.Auto, FanMode.Max, FanMode.Boost, FanMode.Custom });

        public static readonly ReadOnlyCollection<PerformanceLevel> PerformanceChoices = PerformanceController.SelectableLevels;

        public static readonly GameProfile Highest = new GameProfile(FanMode.Boost, PerformanceLevel.Turbo, overclock: true);

        public GameProfile(FanMode fan, PerformanceLevel performance, bool overclock)
        {
            Fan = fan;
            Performance = performance;
            Overclock = overclock;
        }

        public FanMode Fan { get; }
        public PerformanceLevel Performance { get; }

        /// <summary>The user's choice; it only takes effect with Turbo, the one level the GPU offsets are added in.</summary>
        public bool Overclock { get; }

        public bool OverclockApplies => Overclock && Performance == PerformanceLevel.Turbo;

        public GameProfile WithFan(FanMode fan) => new GameProfile(fan, Performance, Overclock);

        public GameProfile WithPerformance(PerformanceLevel performance) => new GameProfile(Fan, performance, Overclock);

        public GameProfile WithOverclock(bool overclock) => new GameProfile(Fan, Performance, overclock);

        /// <summary>The offsets to run the game with, given the user's saved Turbo offsets.</summary>
        public GpuOffsets OffsetsFrom(GpuOffsets saved) => Overclock ? saved : GpuOffsets.None;

        public bool Equals(GameProfile other) =>
            other != null && Fan == other.Fan && Performance == other.Performance && Overclock == other.Overclock;

        public override bool Equals(object obj) => Equals(obj as GameProfile);

        public override int GetHashCode() => Tuple.Create(Fan, Performance, Overclock).GetHashCode();

        public override string ToString() => Performance + (OverclockApplies ? " with GPU overclock" : "") + ", fans " + Fan;
    }
}
