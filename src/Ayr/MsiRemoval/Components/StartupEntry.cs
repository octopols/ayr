using Microsoft.Win32;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>A program Windows starts at sign-in, listed under a Run key.</summary>
    public sealed class StartupEntry : MsiComponent
    {
        private readonly RegistryHive _hive;
        private readonly RegistryView _view;
        private readonly string _runKey;
        private readonly string _valueName;

        public StartupEntry(RegistryHive hive, RegistryView view, string runKey, string valueName, string program)
            : base(MsiComponentKind.StartupEntry, valueName, program)
        {
            _hive = hive;
            _view = view;
            _runKey = runKey;
            _valueName = valueName;
        }

        public override RemovalOutcome Remove(RegistryBackup backup)
        {
            backup.Export(_hive, _view, _runKey);
            using (RegistryKey root = RegistryKey.OpenBaseKey(_hive, _view))
            using (RegistryKey run = root.OpenSubKey(_runKey, writable: true))
            {
                run?.DeleteValue(_valueName, throwOnMissingValue: false);
            }
            return RemovalOutcome.Removed;
        }
    }
}
