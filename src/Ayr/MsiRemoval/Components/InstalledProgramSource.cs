using System.Collections.Generic;
using Microsoft.Win32;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>Windows' installed apps list: the 64-bit and 32-bit machine-wide lists and the current user's.</summary>
    public sealed class InstalledProgramSource : IMsiComponentSource
    {
        private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        private static readonly (RegistryHive Hive, RegistryView View)[] Lists =
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Registry64)
        };

        public IEnumerable<MsiComponent> Find()
        {
            foreach ((RegistryHive hive, RegistryView view) in Lists)
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey list = root.OpenSubKey(UninstallKey))
                {
                    if (list == null)
                    {
                        continue;
                    }
                    foreach (string id in list.GetSubKeyNames())
                    {
                        using (RegistryKey entry = list.OpenSubKey(id))
                        {
                            InstalledProgram program = Read(entry, hive, view, UninstallKey + "\\" + id);
                            if (program != null)
                            {
                                yield return program;
                            }
                        }
                    }
                }
            }
        }

        private static InstalledProgram Read(RegistryKey entry, RegistryHive hive, RegistryView view, string entryKey)
        {
            string name = entry?.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(name) || !MicroStar.IsPublisher(entry.GetValue("Publisher") as string))
            {
                return null;
            }
            string installFolder = (entry.GetValue("InstallLocation") as string)?.Trim().Trim('"').TrimEnd('\\');
            ExternalCommand uninstaller = SilentUninstall.For(entry.GetValue("QuietUninstallString") as string, entry.GetValue("UninstallString") as string);
            return new InstalledProgram(name, hive, view, entryKey, installFolder, uninstaller);
        }
    }
}
