using System;
using System.IO;
using System.Reflection;

namespace Ayr.App
{
    public static class AppPaths
    {
        public static string ExecutablePath => Assembly.GetExecutingAssembly().Location;

        public static string ExecutableDirectory => Path.GetDirectoryName(ExecutablePath);

        public static string DataDirectory
        {
            get
            {
                string directory = Path.Combine(ExecutableDirectory, "data");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public static string VendorDirectory => Path.Combine(ExecutableDirectory, "vendor");

        /// <summary>Whether a registered path (possibly with a "\??\" prefix) points into Ayr's own vendor folder.</summary>
        public static bool PointsIntoVendorDirectory(string registeredPath) =>
            registeredPath != null && registeredPath.IndexOf(VendorDirectory, StringComparison.OrdinalIgnoreCase) >= 0;

        public static string StateFile => Path.Combine(DataDirectory, "state.ini");

        public static string AppleRequestsFile => Path.Combine(DataDirectory, "apple-boosts.ini");

        public static string GamesFile => Path.Combine(DataDirectory, "games.ini");

        public static string CalibrationFile => Path.Combine(DataDirectory, "calibration.ini");

        public static string SnapshotLog => Path.Combine(DataDirectory, "snapshots.log");

        public static string ActivityLog => Path.Combine(DataDirectory, "events.log");

        public static string PreviewDirectory => Path.Combine(DataDirectory, "preview");
    }
}
