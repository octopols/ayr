using System;
using System.IO;

namespace Ayr.Hardware
{
    /// <summary>MSI Super Charger's install folder, which holds the Apple fast-charge helper and driver Ayr adopts.</summary>
    public static class MsiSuperCharger
    {
        public static string Directory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MSI", "Super Charger");
    }
}
