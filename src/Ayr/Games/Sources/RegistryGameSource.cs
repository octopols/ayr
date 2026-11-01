using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Ayr.Games.Sources
{
    /// <summary>A launcher that keeps one registry key per installed game under a common parent key.</summary>
    public abstract class RegistryGameSource : IGameSource
    {
        private readonly RegistryKey _hive;
        private readonly string _parentKey;

        protected RegistryGameSource(RegistryKey hive, string parentKey)
        {
            _hive = hive;
            _parentKey = parentKey;
        }

        public IEnumerable<GameEntry> Find()
        {
            var games = new List<GameEntry>();
            using (RegistryKey parent = _hive.OpenSubKey(_parentKey))
            {
                if (parent == null)
                {
                    return games;
                }
                foreach (string child in parent.GetSubKeyNames())
                {
                    using (RegistryKey record = parent.OpenSubKey(child))
                    {
                        GameEntry game = record == null ? null : Read(record);
                        if (game != null)
                        {
                            games.Add(game);
                        }
                    }
                }
            }
            return games.Where(game => game.IsInstalled);
        }

        /// <summary>The game a record describes, or null when it doesn't describe a usable one.</summary>
        protected abstract GameEntry Read(RegistryKey record);
    }
}
