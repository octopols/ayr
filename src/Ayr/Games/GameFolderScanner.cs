using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ayr.Games
{
    /// <summary>
    /// Portable and copied games no launcher knows about: each folder inside a games folder (such as D:\Games or
    /// D:\Games\Epic Games) that contains a program counts as one game, matched by its whole folder.
    /// </summary>
    public sealed class GameFolderScanner
    {
        // Folders that hold games, at the root of each drive or under Program Files.
        private static readonly string[] LibraryNames = { "Games", "Epic Games", "Riot Games", "GOG Games", "XboxGames", "Ubisoft Games", "EA Games" };
        private static readonly string[] SteamLibraryCommon = { "SteamLibrary", "steamapps", "common" };

        // Launchers and shared runtimes that live next to games but aren't games.
        private static readonly HashSet<string> NotGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Riot Client", "Launcher", "Epic Online Services", "DirectXRedist", "GameSave", "Content", "Uplay", "Ubisoft Game Launcher"
        };

        public List<GameEntry> FindAll() =>
            LibraryFolders()
                .SelectMany(GameFoldersIn)
                .Select(folder => new GameEntry(Path.GetFileName(folder), folder))
                .ToList();

        private static IEnumerable<string> LibraryFolders()
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
            {
                string root = drive.RootDirectory.FullName;
                string[] parents = { root, Path.Combine(root, "Program Files"), Path.Combine(root, "Program Files (x86)") };
                foreach (string parent in parents)
                {
                    foreach (string name in LibraryNames)
                    {
                        yield return Path.Combine(parent, name);
                    }
                }
                yield return Path.Combine(new[] { root }.Concat(SteamLibraryCommon).ToArray());
            }
        }

        // A library can hold another library ("Games\Epic Games"); those are opened one level further.
        private static IEnumerable<string> GameFoldersIn(string library)
        {
            foreach (string folder in Subfolders(library).Where(folder => !NotGames.Contains(Path.GetFileName(folder))))
            {
                if (LibraryNames.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase))
                {
                    foreach (string nested in Subfolders(folder).Where(nested => !NotGames.Contains(Path.GetFileName(nested)) && ContainsProgram(nested)))
                    {
                        yield return nested;
                    }
                }
                else if (ContainsProgram(folder))
                {
                    yield return folder;
                }
            }
        }

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
