using System;
using System.IO;

namespace Ayr.Hardware
{
    /// <summary>MSI Foundation Service's install folder, which holds the vendor libraries ayrctl loads.</summary>
    public static class MsiFoundationService
    {
        public static string Directory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MSI", "MSI NBFoundation Service");
    }
}
