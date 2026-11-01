using System.Collections.Generic;
using Microsoft.Win32;

namespace Ayr.MsiRemoval.Components
{
    public sealed class RegistrySettingsSource : IMsiComponentSource
    {
        private const string MsiKey = @"SOFTWARE\MSI";

        private static readonly (RegistryHive Hive, RegistryView View, string DisplayPath)[] Locations =
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"HKLM\SOFTWARE\MSI"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"HKLM\SOFTWARE\WOW6432Node\MSI"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, @"HKCU\SOFTWARE\MSI")
        };

        public IEnumerable<MsiComponent> Find()
        {
            foreach ((RegistryHive hive, RegistryView view, string displayPath) in Locations)
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey settings = root.OpenSubKey(MsiKey))
                {
                    if (settings != null)
                    {
                        yield return new RegistrySettings(hive, view, MsiKey, displayPath);
                    }
                }
            }
        }
    }
}
