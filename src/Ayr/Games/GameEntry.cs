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
            Path = System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        }

        public string Name { get; }

        /// <summary>A full path without a trailing separator.</summary>
        public string Path { get; }

        public bool IsInstalled => IsExecutable ? File.Exists(Path) : Directory.Exists(Path);

        private bool IsExecutable => Path.EndsWith(ExecutableExtension, StringComparison.OrdinalIgnoreCase);

        public bool Matches(string processPath) => IsExecutable
            ? string.Equals(processPath, Path, StringComparison.OrdinalIgnoreCase)
            : processPath.StartsWith(Path + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        public bool SamePathAs(GameEntry other) => string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether either one covers the other, such as a game's folder and the .exe inside it.</summary>
        public bool Overlaps(GameEntry other) => SamePathAs(other) || Matches(other.Path) || other.Matches(Path);
    }
}
