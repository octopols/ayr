using System;
using System.Collections.Generic;
using System.Linq;
using Ayr.Games.Sources;

namespace Ayr.Games
{
    /// <summary>
    /// Games on this PC, so the user can pick one by name instead of hunting for the executable that actually runs it.
    /// Launcher records come first: they have the exact install folder and the real name. A game folder found only by the
    /// folder scan takes the name Windows knows its program by, when Windows has seen it run.
    /// </summary>
    public sealed class InstalledGameFinder
    {
        private readonly IGameSource[] _launchers =
        {
            new RiotGameSource(), new SteamGameSource(), new EpicGameSource(), new GogGameSource(), new UbisoftGameSource()
        };
        private readonly IGameSource _playedOnThisPc = new WindowsPlayedGameSource();
        private readonly IGameSource _gameFolders = new GameFolderSource();

        public List<GameEntry> FindAll()
        {
            List<GameEntry> found = _launchers.SelectMany(source => source.Find()).ToList();
            List<GameEntry> played = _playedOnThisPc.Find().Where(game => !found.Any(game.Overlaps)).ToList();
            foreach (GameEntry folder in _gameFolders.Find().Where(folder => !IsKnown(folder, found)))
            {
                GameEntry program = played.FirstOrDefault(folder.Overlaps);
                played.Remove(program);
                found.Add(new GameEntry(program?.Name ?? folder.Name, folder.Path));
            }
            return found.Concat(played)
                .GroupBy(game => game.Path, StringComparer.OrdinalIgnoreCase)
                .Select(samePath => samePath.First())
                .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // A copy elsewhere of a game a launcher already lists, such as an old VALORANT folder, would only be a duplicate.
        private static bool IsKnown(GameEntry folder, List<GameEntry> found) =>
            found.Any(known => known.Overlaps(folder) || string.Equals(known.Name, folder.Name, StringComparison.OrdinalIgnoreCase));
    }
}
