using System;
using System.IO;

namespace Ayr.Games
{
    /// <summary>
    /// A game Ayr watches for: an install folder, so every process the game starts from it counts (launchers often hand
    /// over to a differently named executable), or a single .exe added by hand.
    /// </summary>
    public sealed class GameEntry
    {
        private const string ExecutableExtension = ".exe";

        public GameEntry(string name, string path)
        {
            Name = name;
            Path = NormalizePath(path);
        }

        public string Name { get; }

        /// <summary>A full path without a trailing separator.</summary>
        public string Path { get; }

        public bool IsInstalled => IsExecutable ? File.Exists(Path) : Directory.Exists(Path);

        private bool IsExecutable => Path.EndsWith(ExecutableExtension, StringComparison.OrdinalIgnoreCase);

        /// <summary>Null when the record has no name or no usable path, as in a launcher's damaged record.</summary>
        public static GameEntry TryCreate(string name, string path)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            try
            {
                return new GameEntry(name.Trim(), path);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return null;
            }
        }

        public static string NormalizePath(string path) =>
            System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar);

        public bool Matches(string processPath) => IsExecutable
            ? string.Equals(processPath, Path, StringComparison.OrdinalIgnoreCase)
            : processPath.StartsWith(Path + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        public bool SamePathAs(GameEntry other) => string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether either one covers the other, such as a game's folder and the .exe inside it.</summary>
        public bool Overlaps(GameEntry other) => SamePathAs(other) || Matches(other.Path) || other.Matches(Path);
    }
}
