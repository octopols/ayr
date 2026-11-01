using System;
using System.IO;
using Ayr.App;
using Microsoft.Win32;

namespace Ayr.MsiRemoval
{
    /// <summary>
    /// Exports every registry key before it is deleted, as .reg files in one folder per removal, so anything removed by
    /// mistake can be put back with a double-click. Nothing is deleted when its export fails.
    /// </summary>
    public sealed class RegistryBackup
    {
        private static readonly TimeSpan ExportTimeout = TimeSpan.FromMinutes(1);

        private int _exported;

        public RegistryBackup()
        {
            Folder = Path.Combine(AppPaths.DataDirectory, "msi-removal", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(Folder);
        }

        public string Folder { get; }

        public void Export(RegistryHive hive, RegistryView view, string subKey)
        {
            _exported++;
            string file = Path.Combine(Folder, _exported.ToString("D3") + " " + Path.GetFileName(subKey.TrimEnd('\\')) + ".reg");
            string arguments = "export \"" + HiveName(hive) + "\\" + subKey + "\" \"" + file + "\" /y " + ViewSwitch(view);
            new ExternalCommand("reg.exe", arguments).RunOrThrow(ExportTimeout);
        }

        private static string HiveName(RegistryHive hive)
        {
            switch (hive)
            {
                case RegistryHive.LocalMachine:
                    return "HKLM";
                case RegistryHive.CurrentUser:
                    return "HKCU";
                default:
                    throw new ArgumentOutOfRangeException(nameof(hive), hive, "Only HKLM and HKCU are backed up.");
            }
        }

        private static string ViewSwitch(RegistryView view) => view == RegistryView.Registry32 ? "/reg:32" : "/reg:64";
    }
}
