using System.IO;
using Microsoft.Win32;

namespace Ayr.Games.Sources
{
    /// <summary>
    /// Every program Windows recognized as a game when it ran (the list Game Bar uses), so portable games no launcher knows
    /// about are found too, once they have been played on this PC.
    /// </summary>
    public sealed class WindowsPlayedGameSource : RegistryGameSource
    {
        public WindowsPlayedGameSource() : base(Registry.CurrentUser, @"System\GameConfigStore\Children")
        {
        }

        protected override GameEntry Read(RegistryKey record)
        {
            string executable = record.GetValue("MatchedExeFullPath") as string;
            return string.IsNullOrEmpty(executable) || !File.Exists(executable)
                ? null
                : GameEntry.TryCreate(ExecutableName.For(executable), executable);
        }
    }
}
