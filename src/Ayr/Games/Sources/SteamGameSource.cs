using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Ayr.Games.Sources
{
    /// <summary>Steam lists its libraries (one per drive) in libraryfolders.vdf, and each game in an appmanifest file.</summary>
    public sealed class SteamGameSource : IGameSource
    {
        // Steam's shared installers (DirectX, VC++ runtimes): installed alongside games, but not a game.
        private const string RedistributablesManifest = "appmanifest_228980.acf";

        private static readonly Regex LibraryPath = new Regex(@"""path""\s+""([^""]+)""");
        private static readonly Regex GameName = new Regex(@"""name""\s+""([^""]+)""");
        private static readonly Regex InstallFolder = new Regex(@"""installdir""\s+""([^""]+)""");

        public IEnumerable<GameEntry> Find()
        {
            string libraries = LibraryListFile();
            if (libraries == null)
            {
                return Enumerable.Empty<GameEntry>();
            }
            return LibraryPath.Matches(File.ReadAllText(libraries)).Cast<Match>()
                .Select(match => Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), "steamapps"))
                .Where(Directory.Exists)
                .SelectMany(GamesIn)
                .Where(game => game != null && game.IsInstalled)
                .ToList();
        }

        private static string LibraryListFile()
        {
            using (RegistryKey steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                string steamFolder = steam?.GetValue("SteamPath") as string;
                string file = steamFolder == null ? null : Path.Combine(steamFolder, "steamapps", "libraryfolders.vdf");
                return file != null && File.Exists(file) ? file : null;
            }
        }

        private static IEnumerable<GameEntry> GamesIn(string apps) =>
            Directory.GetFiles(apps, "appmanifest_*.acf")
                .Where(manifest => !Path.GetFileName(manifest).Equals(RedistributablesManifest, StringComparison.OrdinalIgnoreCase))
                .Select(manifest => Read(apps, File.ReadAllText(manifest)));

        private static GameEntry Read(string apps, string manifest)
        {
            Match name = GameName.Match(manifest);
            Match folder = InstallFolder.Match(manifest);
            return name.Success && folder.Success
                ? GameEntry.TryCreate(name.Groups[1].Value, Path.Combine(apps, "common", folder.Groups[1].Value))
                : null;
        }
    }
}
