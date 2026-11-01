using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ayr.App;
using Ayr.Controllers;

namespace Ayr.Games
{
    /// <summary>
    /// Game mode: while one of the user's games runs, the supervisor switches to Turbo (with the saved GPU overclock) and
    /// Cooler Boost, and puts the everyday settings back once the last game closes. This class manages the setting and the
    /// game list; the switching itself happens in the supervisor.
    /// </summary>
    public sealed class GameModeController
    {
        public const FanMode GamingFan = FanMode.Boost;
        public const PerformanceLevel GamingPerformance = PerformanceLevel.Turbo;

        // Finding games walks the game folders on every drive, too slow for each refresh of an open window.
        private static readonly TimeSpan FoundGamesLifetime = TimeSpan.FromMinutes(2);

        private readonly GameLibrary _library = new GameLibrary();
        private readonly InstalledGameFinder _finder = new InstalledGameFinder();
        private List<GameEntry> _found;
        private DateTime _foundAt = DateTime.MinValue;

        public GameOverview Overview()
        {
            List<GameEntry> added = _library.Load();
            List<string> processes = ProcessPaths.All().Values.ToList();
            List<GameEntry> running = added.Where(game => processes.Any(game.Matches)).ToList();
            List<GameEntry> notAdded = FoundGames().Where(candidate => !added.Any(candidate.Overlaps)).ToList();
            return new GameOverview(added, running, notAdded);
        }

        /// <summary>The first time it is switched on with no games added, the games found on this PC are added, so it just works.</summary>
        public void SetEnabled(bool on)
        {
            DesiredState.Update(state => state.GameMode = on);
            if (on && _library.Load().Count == 0)
            {
                foreach (GameEntry game in FoundGames())
                {
                    _library.Add(game);
                }
            }
            ActivityLog.Write("game mode " + (on ? "on" : "off"));
        }

        public void Add(GameEntry game)
        {
            _library.Add(game);
            ActivityLog.Write("game added: " + game.Name + " (" + game.Path + ")");
        }

        /// <summary>
        /// Adds an install folder or a single .exe. Picking a program inside a game Ayr found adds that whole game instead,
        /// because games often run a different program than the one people click.
        /// </summary>
        public GameEntry AddPath(string path)
        {
            var candidate = new GameEntry(NameFor(path), path);
            if (!candidate.IsInstalled)
            {
                throw new FileNotFoundException("No game folder or .exe at " + candidate.Path + ".", candidate.Path);
            }
            GameEntry game = FoundGames().FirstOrDefault(found => found.SamePathAs(candidate) || found.Matches(candidate.Path)) ?? candidate;
            Add(game);
            return game;
        }

        public void Remove(GameEntry game)
        {
            _library.Remove(game);
            ActivityLog.Write("game removed: " + game.Name);
        }

        public GameEntry FindAdded(string path)
        {
            var wanted = new GameEntry("", path);
            return _library.Load().FirstOrDefault(wanted.SamePathAs);
        }

        /// <summary>Looks for games again now rather than when the cached list expires; returns how many were found.</summary>
        public int ScanAgain()
        {
            _found = null;
            return FoundGames().Count;
        }

        private List<GameEntry> FoundGames()
        {
            if (_found == null || DateTime.Now - _foundAt > FoundGamesLifetime)
            {
                _found = _finder.FindAll();
                _foundAt = DateTime.Now;
            }
            return _found;
        }

        private static string NameFor(string path) =>
            File.Exists(path) ? ExecutableName.For(path) : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
    }
}
