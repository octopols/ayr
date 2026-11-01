using System.Collections.Generic;

namespace Ayr.Games
{
    public sealed class GameOverview
    {
        public GameOverview(IReadOnlyList<GameEntry> added, IReadOnlyList<GameEntry> running, IReadOnlyList<GameEntry> foundNotAdded)
        {
            Added = added;
            Running = running;
            FoundNotAdded = foundNotAdded;
        }

        public IReadOnlyList<GameEntry> Added { get; }

        /// <summary>The added games that are running right now.</summary>
        public IReadOnlyList<GameEntry> Running { get; }

        /// <summary>Games installed through a launcher that the user hasn't added yet.</summary>
        public IReadOnlyList<GameEntry> FoundNotAdded { get; }
    }
}
