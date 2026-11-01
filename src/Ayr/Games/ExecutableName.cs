using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Ayr.Games
{
    /// <summary>A readable game name for an .exe, from its version info or, failing that, its folders.</summary>
    public static class ExecutableName
    {
        // Unreal Engine games run from <game>\<project>\Binaries\Win64\<project>-Win64-Shipping.exe and carry no version info.
        private const string UnrealBinaries = @"\Binaries\Win64\";
        private const int UnrealLevelsAboveGame = 3;

        public static string For(string executablePath)
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(executablePath);
            // The description is usually the game's title; the product name is sometimes the publisher's platform instead.
            string fromVersionInfo = new[] { info.FileDescription, info.ProductName }.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
            if (fromVersionInfo != null)
            {
                return fromVersionInfo.Trim();
            }
            if (executablePath.IndexOf(UnrealBinaries, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DirectoryInfo game = new FileInfo(executablePath).Directory;
                for (int level = 0; level < UnrealLevelsAboveGame && game.Parent != null; level++)
                {
                    game = game.Parent;
                }
                return game.Name;
            }
            return Path.GetFileNameWithoutExtension(executablePath);
        }
    }
}
