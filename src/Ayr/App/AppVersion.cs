using System;
using System.Reflection;

namespace Ayr.App
{
    public static class AppVersion
    {
        /// <summary>The release version (major.minor.patch) the build was stamped with.</summary>
        public static Version Current => Normalize(Assembly.GetExecutingAssembly().GetName().Version);

        /// <summary>Assembly versions carry a fourth part that releases don't, and Version compares 0.1.0.0 above 0.1.0.</summary>
        public static Version Normalize(Version version) => new Version(version.Major, version.Minor, version.Build);
    }
}
