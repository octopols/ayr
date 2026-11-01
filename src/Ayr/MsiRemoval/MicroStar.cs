using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ayr.App;

namespace Ayr.MsiRemoval
{
    /// <summary>
    /// Decides what belongs to MSI. "MSI" in a name proves nothing (Windows Installer's own service is msiserver), so only
    /// the publisher, a file's company or MSI's install folders count. Ayr's own folder never counts, even though it holds
    /// MSI-signed files.
    /// </summary>
    public static class MicroStar
    {
        private static readonly string[] CompanyNames = { "Micro-Star", "Micro Star" };
        private static readonly string[] ShortPublisherNames = { "MSI", "MSI Co., LTD" };

        public static IReadOnlyList<string> Folders { get; } = new[]
        {
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.CommonApplicationData,
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.ApplicationData
        }.Select(folder => Path.Combine(Environment.GetFolderPath(folder), "MSI")).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public static bool IsPublisher(string publisher)
        {
            if (string.IsNullOrWhiteSpace(publisher))
            {
                return false;
            }
            string trimmed = publisher.Trim();
            return CompanyNames.Any(name => trimmed.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                || ShortPublisherNames.Any(name => trimmed.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static bool OwnsFile(string path)
        {
            if (string.IsNullOrEmpty(path) || IsAyrPath(path))
            {
                return false;
            }
            return IsInMsiFolder(path) || (File.Exists(path) && IsPublisher(FileVersionInfo.GetVersionInfo(path).CompanyName));
        }

        public static bool IsInMsiFolder(string path) => Folders.Any(folder => FolderPath.Contains(folder, path));

        public static bool IsAyrPath(string path) => FolderPath.Contains(AppPaths.ExecutableDirectory, path);
    }
}
