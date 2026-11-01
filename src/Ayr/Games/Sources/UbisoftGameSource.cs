using System.IO;
using Microsoft.Win32;

namespace Ayr.Games.Sources
{
    public sealed class UbisoftGameSource : RegistryGameSource
    {
        // Ubisoft Connect is 32-bit, so its records live in the 32-bit view of the registry.
        public UbisoftGameSource() : base(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs")
        {
        }

        // Ubisoft records only the install folder, which is named after the game.
        protected override GameEntry Read(RegistryKey record)
        {
            string folder = (record.GetValue("InstallDir") as string)?.TrimEnd('/', '\\');
            return GameEntry.TryCreate(Path.GetFileName(folder), folder);
        }
    }
}
