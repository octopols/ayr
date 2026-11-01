using Microsoft.Win32;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>A registry key where MSI's software keeps its settings.</summary>
    public sealed class RegistrySettings : MsiComponent
    {
        private readonly RegistryHive _hive;
        private readonly RegistryView _view;
        private readonly string _key;

        public RegistrySettings(RegistryHive hive, RegistryView view, string key, string displayPath)
            : base(MsiComponentKind.Settings, "MSI settings", displayPath)
        {
            _hive = hive;
            _view = view;
            _key = key;
        }

        public override RemovalOutcome Remove(RegistryBackup backup)
        {
            backup.Export(_hive, _view, _key);
            using (RegistryKey root = RegistryKey.OpenBaseKey(_hive, _view))
            {
                root.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
            }
            return RemovalOutcome.Removed;
        }
    }
}
