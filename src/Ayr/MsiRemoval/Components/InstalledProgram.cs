using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Ayr.App;
using Microsoft.Win32;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// A program in Windows' installed apps list. Its own uninstaller runs first, silently; whatever it leaves behind, its
    /// list entry and install folder, is then removed directly. Super Charger's uninstaller is never run: it stops MSI's
    /// Apple charging driver, and stopping that driver has crashed Windows.
    /// </summary>
    public sealed class InstalledProgram : MsiComponent
    {
        private const string AppleChargeDriverPattern = "ipadtst*.sys";
        private static readonly TimeSpan UninstallerTimeout = TimeSpan.FromMinutes(10);
        // Some uninstallers hand over to a copy of themselves in %TEMP% and exit before the work is done.
        private static readonly TimeSpan HandOverGrace = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
        private static readonly string[] StandaloneTools = { "Afterburner", "Kombustor" };

        private readonly RegistryHive _hive;
        private readonly RegistryView _view;
        private readonly string _entryKey;
        private readonly string _installFolder;
        private readonly ExternalCommand _uninstaller;

        public InstalledProgram(string name, RegistryHive hive, RegistryView view, string entryKey, string installFolder, ExternalCommand uninstaller)
            : base(MsiComponentKind.Program, name, string.IsNullOrEmpty(installFolder) ? entryKey : installFolder)
        {
            _hive = hive;
            _view = view;
            _entryKey = entryKey;
            _installFolder = installFolder;
            _uninstaller = uninstaller;
        }

        /// <summary>Afterburner and Kombustor are GPU tools people use on any brand of laptop, so they are only removed on request.</summary>
        public override bool SelectedByDefault => !StandaloneTools.Any(tool => Name.IndexOf(tool, StringComparison.OrdinalIgnoreCase) >= 0);

        public override RemovalOutcome Remove(RegistryBackup backup)
        {
            backup.Export(_hive, _view, _entryKey);
            if (_uninstaller != null && !InstallsAppleChargeDriver)
            {
                RunUninstaller();
            }
            if (EntryExists)
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(_hive, _view))
                {
                    root.DeleteSubKeyTree(_entryKey, throwOnMissingSubKey: false);
                }
            }
            bool deferred = InstallFolderExists && FileEraser.DeleteFolder(_installFolder);
            return deferred ? RemovalOutcome.RemovedAtRestart : RemovalOutcome.Removed;
        }

        private bool InstallFolderExists => !string.IsNullOrEmpty(_installFolder) && Directory.Exists(_installFolder) && MicroStar.IsInMsiFolder(_installFolder);

        // The name is checked too, in case the entry doesn't say where it was installed.
        private bool InstallsAppleChargeDriver =>
            (Name.IndexOf("Super", StringComparison.OrdinalIgnoreCase) >= 0 && Name.IndexOf("Charger", StringComparison.OrdinalIgnoreCase) >= 0)
            || (InstallFolderExists && Directory.EnumerateFiles(_installFolder, AppleChargeDriverPattern, SearchOption.AllDirectories).Any());

        private bool EntryExists
        {
            get
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(_hive, _view))
                using (RegistryKey entry = root.OpenSubKey(_entryKey))
                {
                    return entry != null;
                }
            }
        }

        // An uninstaller whose own files are already gone can't run; the direct removal that follows still cleans up.
        private void RunUninstaller()
        {
            try
            {
                _uninstaller.Run(UninstallerTimeout, out _);
            }
            catch (Win32Exception ex)
            {
                ActivityLog.Write("uninstaller for " + Name + " could not start (" + ex.Message + "); removing it directly");
                return;
            }
            WaitForEntryToGo();
        }

        private void WaitForEntryToGo()
        {
            DateTime giveUpAt = DateTime.Now + HandOverGrace;
            while (EntryExists && DateTime.Now < giveUpAt)
            {
                Thread.Sleep(PollInterval);
            }
        }
    }
}
