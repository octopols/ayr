using Microsoft.Win32;

namespace Ayr.Games.Sources
{
    public sealed class GogGameSource : RegistryGameSource
    {
        // GOG Galaxy is 32-bit, so its records live in the 32-bit view of the registry.
        public GogGameSource() : base(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\GOG.com\Games")
        {
        }

        protected override GameEntry Read(RegistryKey record) =>
            GameEntry.TryCreate(record.GetValue("gameName") as string, record.GetValue("path") as string);
    }
}
