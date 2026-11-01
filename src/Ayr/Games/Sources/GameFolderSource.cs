using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ayr.Games.Sources
{
    /// <summary>
    /// Portable and copied games no launcher knows about: each folder inside a games folder (such as D:\Games or
    /// D:\Games\Epic Games) that contains a program counts as one game, matched by its whole folder.
    /// </summary>
    public sealed class GameFolderSource : IGameSource
    {
        private static readonly string[] LibraryNames = { "Games", "Epic Games", "Riot Games", "GOG Games", "XboxGames", "Ubisoft Games", "EA Games" };

        // Launchers and shared runtimes that sit next to games but aren't games.
        private static readonly HashSet<string> NotGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Riot Client", "Launcher", "Epic Online Services", "DirectXRedist", "Ubisoft Game Launcher"
        };

        public IEnumerable<GameEntry> Find() =>
            LibraryFolders()
                .SelectMany(GameFoldersIn)
                .Select(folder => GameEntry.TryCreate(Path.GetFileName(folder), folder))
                .Where(game => game != null)
                .ToList();

        private static IEnumerable<string> LibraryFolders()
        {
            IEnumerable<DriveInfo> drives = DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady);
            foreach (string root in drives.Select(drive => drive.RootDirectory.FullName))
            {
                foreach (string parent in new[] { root, Path.Combine(root, "Program Files"), Path.Combine(root, "Program Files (x86)") })
                {
                    foreach (string name in LibraryNames)
                    {
                        yield return Path.Combine(parent, name);
                    }
                }
                yield return Path.Combine(root, "SteamLibrary", "steamapps", "common");
            }
        }

        // A library can hold another library ("Games\Epic Games"); that one is opened one level further.
        private static IEnumerable<string> GameFoldersIn(string library)
        {
            foreach (string folder in Candidates(library))
            {
                bool nestedLibrary = LibraryNames.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase);
                IEnumerable<string> games = nestedLibrary ? Candidates(folder) : new[] { folder };
                foreach (string game in games.Where(ContainsProgram))
                {
                    yield return game;
                }
            }
        }

        private static IEnumerable<string> Candidates(string library) =>
            Subfolders(library).Where(folder => !NotGames.Contains(Path.GetFileName(folder)));

        private static string[] Subfolders(string folder)
        {
            try
            {
                return Directory.Exists(folder) ? Directory.GetDirectories(folder) : new string[0];
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                return new string[0];
            }
        }

        private static bool ContainsProgram(string folder)
        {
            try
            {
                return Directory.EnumerateFiles(folder, "*.exe", SearchOption.AllDirectories).Any();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                return false;
            }
        }
    }
}
