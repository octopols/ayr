using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ayr.Games.Sources
{
    /// <summary>Riot Client keeps one settings file per installed product under ProgramData.</summary>
    public sealed class RiotGameSource : IGameSource
    {
        private const string SettingsSuffix = ".product_settings.yaml";
        private const string TestServerSuffix = ".pbe";
        private static readonly Regex ScalarLine = new Regex(@"^(\w+):\s*""?(.*?)""?\s*$");

        public IEnumerable<GameEntry> Find()
        {
            string metadata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "Metadata");
            if (!Directory.Exists(metadata))
            {
                return Enumerable.Empty<GameEntry>();
            }
            return Directory.GetFiles(metadata, "*" + SettingsSuffix, SearchOption.AllDirectories)
                .Select(Read)
                .Where(game => game != null && game.IsInstalled)
                .ToList();
        }

        private static GameEntry Read(string settingsFile)
        {
            Dictionary<string, string> settings = ReadScalars(settingsFile);
            settings.TryGetValue("product_install_full_path", out string installFolder);
            settings.TryGetValue("product_install_root", out string root);
            settings.TryGetValue("shortcut_name", out string shortcut);
            if (installFolder == null || root == null)
            {
                return null;
            }
            string product = Path.GetFileName(settingsFile).Substring(0, Path.GetFileName(settingsFile).Length - SettingsSuffix.Length);
            return GameEntry.TryCreate(Name(product, shortcut, installFolder, root), installFolder);
        }

        // VALORANT names its shortcut. The others are named after their folder under the Riot Games root, which a game's live
        // and test-server (PBE) installs share.
        private static string Name(string product, string shortcut, string installFolder, string root)
        {
            if (shortcut != null)
            {
                return Path.GetFileNameWithoutExtension(shortcut);
            }
            string underRoot = installFolder.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? installFolder.Substring(root.Length) : installFolder;
            string name = underRoot.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return product.EndsWith(TestServerSuffix, StringComparison.OrdinalIgnoreCase) ? name + " (PBE)" : name;
        }

        private static Dictionary<string, string> ReadScalars(string file)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match line in File.ReadAllLines(file).Select(text => ScalarLine.Match(text)).Where(match => match.Success))
            {
                if (line.Groups[2].Value.Length > 0)
                {
                    values[line.Groups[1].Value] = line.Groups[2].Value;
                }
            }
            return values;
        }
    }
}
