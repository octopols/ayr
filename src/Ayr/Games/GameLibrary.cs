using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ayr.App;

namespace Ayr.Games
{
    /// <summary>The games the user picked, kept in data\games.ini as "path=name" lines.</summary>
    public sealed class GameLibrary
    {
        private readonly KeyValueFile _file = new KeyValueFile(AppPaths.GamesFile);

        /// <summary>Changes whenever the list is saved, so a watcher can tell when to reload it.</summary>
        public DateTime Version => File.Exists(AppPaths.GamesFile) ? File.GetLastWriteTimeUtc(AppPaths.GamesFile) : DateTime.MinValue;

        public List<GameEntry> Load() =>
            _file.Read()
                .Select(entry => new GameEntry(entry.Value, entry.Key))
                .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        public void Add(GameEntry game)
        {
            Dictionary<string, string> entries = _file.Read();
            // The file splits each line at its last '=', so a name must not contain one.
            entries[game.Path] = game.Name.Replace("=", " ");
            _file.Write(entries);
        }

        public void Remove(GameEntry game)
        {
            Dictionary<string, string> entries = _file.Read();
            if (entries.Remove(game.Path))
            {
                _file.Write(entries);
            }
        }
    }
}
