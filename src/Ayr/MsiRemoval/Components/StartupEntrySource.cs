using System.Collections.Generic;
using Microsoft.Win32;

namespace Ayr.MsiRemoval.Components
{
    public sealed class StartupEntrySource : IMsiComponentSource
    {
        private static readonly (RegistryHive Hive, RegistryView View, string Key)[] RunKeys =
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce")
        };

        public IEnumerable<MsiComponent> Find()
        {
            foreach ((RegistryHive hive, RegistryView view, string key) in RunKeys)
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey run = root.OpenSubKey(key))
                {
                    if (run == null)
                    {
                        continue;
                    }
                    foreach (string valueName in run.GetValueNames())
                    {
                        string program = CommandPath.ProgramOf(run.GetValue(valueName) as string);
                        if (MicroStar.OwnsFile(program))
                        {
                            yield return new StartupEntry(hive, view, key, valueName, program);
                        }
                    }
                }
            }
        }
    }
}
