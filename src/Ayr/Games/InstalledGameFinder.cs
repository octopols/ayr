using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Ayr.Games
{
    /// <summary>
    /// Games on this PC, so the user can pick one by name instead of hunting for the executable that actually runs it:
    /// from the Riot, Steam and Epic launchers' own records, and from Windows' list of programs it recognized as games.
    /// </summary>
    public sealed class InstalledGameFinder
    {
        private const string RiotSettingsPattern = "*.product_settings.yaml";
        private const string RiotTestServerSuffix = ".pbe";
        private const string SteamKey = @"Software\Valve\Steam";
        private const string WindowsGameStoreKey = @"System\GameConfigStore\Children";
        // Steam's shared installers (DirectX, VC++ runtimes), installed alongside games but not a game.
        private const string SteamRedistributablesManifest = "appmanifest_228980.acf";

        private static readonly Regex YamlLine = new Regex("^(\\w+):\\s*\"?(.*?)\"?\\s*$");
        private static readonly Regex VdfPath = new Regex("\"path\"\\s+\"([^\"]+)\"");
        private static readonly Regex AcfName = new Regex("\"name\"\\s+\"([^\"]+)\"");
        private static readonly Regex AcfInstallDirectory = new Regex("\"installdir\"\\s+\"([^\"]+)\"");

        private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        private readonly GameFolderScanner _folders = new GameFolderScanner();

        /// <summary>
        /// Launcher records come first: they have the exact install folder and the real name. A game folder that only the
        /// scan found takes the name Windows knows its program by, when Windows has seen it run.
        /// </summary>
        public List<GameEntry> FindAll()
        {
            List<GameEntry> found = Riot().Concat(Steam()).Concat(Epic()).Where(game => game.IsInstalled).ToList();
            List<GameEntry> played = RecognizedByWindows()
                .Where(game => game.IsInstalled && !found.Any(known => known.Overlaps(game)))
                .ToList();
            foreach (GameEntry folder in _folders.FindAll())
            {
                bool alreadyKnown = found.Any(known => known.Overlaps(folder)
                    || string.Equals(known.Name, folder.Name, StringComparison.OrdinalIgnoreCase));
                if (alreadyKnown)
                {
                    continue;
                }
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

        /// <summary>
        /// Every program Windows has recognized as a game when it ran (the list Game Bar uses), so portable games that no
        /// launcher knows about are found too, once they have been played on this PC.
        /// </summary>
        private static IEnumerable<GameEntry> RecognizedByWindows()
        {
            using (RegistryKey store = Registry.CurrentUser.OpenSubKey(WindowsGameStoreKey))
            {
                if (store == null)
                {
                    yield break;
                }
                foreach (string child in store.GetSubKeyNames())
                {
                    string executable;
                    using (RegistryKey game = store.OpenSubKey(child))
                    {
                        executable = game?.GetValue("MatchedExeFullPath") as string;
                    }
                    if (!string.IsNullOrEmpty(executable) && File.Exists(executable))
                    {
                        yield return new GameEntry(ExecutableName.For(executable), executable);
                    }
                }
            }
        }

        private static IEnumerable<GameEntry> Riot()
        {
            string metadata = Path.Combine(ProgramData, "Riot Games", "Metadata");
            if (!Directory.Exists(metadata))
            {
                yield break;
            }
            foreach (string settingsFile in Directory.GetFiles(metadata, RiotSettingsPattern, SearchOption.AllDirectories))
            {
                Dictionary<string, string> settings = ReadYamlScalars(settingsFile);
                if (!settings.TryGetValue("product_install_full_path", out string installPath)
                    || !settings.TryGetValue("product_install_root", out string root))
                {
                    continue;
                }
                yield return new GameEntry(RiotName(settingsFile, settings, installPath, root), installPath);
            }
        }

        // VALORANT names its shortcut; the others are named after their folder under the Riot Games root.
        private static string RiotName(string settingsFile, Dictionary<string, string> settings, string installPath, string root)
        {
            if (settings.TryGetValue("shortcut_name", out string shortcut))
            {
                return Path.GetFileNameWithoutExtension(shortcut);
            }
            string relative = installPath.Substring(Math.Min(root.Length, installPath.Length)).Trim('/', '\\');
            string name = relative.Split('/', '\\')[0];
            string product = Path.GetFileName(settingsFile).Replace(RiotSettingsPattern.TrimStart('*'), "");
            return product.EndsWith(RiotTestServerSuffix, StringComparison.OrdinalIgnoreCase) && !name.Contains("PBE") ? name + " (PBE)" : name;
        }

        private static Dictionary<string, string> ReadYamlScalars(string file)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(file))
            {
                Match match = YamlLine.Match(line);
                if (match.Success && match.Groups[2].Value.Length > 0)
                {
                    values[match.Groups[1].Value] = match.Groups[2].Value;
                }
            }
            return values;
        }

        private static IEnumerable<GameEntry> Steam()
        {
            string steam;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SteamKey))
            {
                steam = key?.GetValue("SteamPath") as string;
            }
            string libraries = steam == null ? null : Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (libraries == null || !File.Exists(libraries))
            {
                yield break;
            }
            IEnumerable<string> libraryFolders = VdfPath.Matches(File.ReadAllText(libraries)).Cast<Match>()
                .Select(match => match.Groups[1].Value.Replace(@"\\", @"\"));
            foreach (string library in libraryFolders)
            {
                string apps = Path.Combine(library, "steamapps");
                if (!Directory.Exists(apps))
                {
                    continue;
                }
                foreach (string manifest in Directory.GetFiles(apps, "appmanifest_*.acf"))
                {
                    if (Path.GetFileName(manifest).Equals(SteamRedistributablesManifest, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string text = File.ReadAllText(manifest);
                    Match name = AcfName.Match(text);
                    Match folder = AcfInstallDirectory.Match(text);
                    if (name.Success && folder.Success)
                    {
                        yield return new GameEntry(name.Groups[1].Value, Path.Combine(apps, "common", folder.Groups[1].Value));
                    }
                }
            }
        }

        private static IEnumerable<GameEntry> Epic()
        {
            string manifests = Path.Combine(ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifests))
            {
                yield break;
            }
            var serializer = new DataContractJsonSerializer(typeof(EpicManifest));
            foreach (string file in Directory.GetFiles(manifests, "*.item"))
            {
                EpicManifest manifest;
                try
                {
                    using (FileStream json = File.OpenRead(file))
                    {
                        manifest = (EpicManifest)serializer.ReadObject(json);
                    }
                }
                catch (SerializationException)
                {
                    continue;
                }
                if (manifest.IsApplication && !string.IsNullOrEmpty(manifest.DisplayName) && !string.IsNullOrEmpty(manifest.InstallLocation))
                {
                    yield return new GameEntry(manifest.DisplayName, manifest.InstallLocation);
                }
            }
        }
    }
}
