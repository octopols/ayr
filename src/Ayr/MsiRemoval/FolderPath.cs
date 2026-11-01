using System;

namespace Ayr.MsiRemoval
{
    public static class FolderPath
    {
        /// <summary>Whether the path is the folder itself or anything inside it.</summary>
        public static bool Contains(string folder, string path) =>
            path.Equals(folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
