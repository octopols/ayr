using System.IO;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>One of MSI's top-level folders, with everything its programs left in it.</summary>
    public sealed class MsiFolder : MsiComponent
    {
        public MsiFolder(string path) : base(MsiComponentKind.Folder, "MSI files", path)
        {
        }

        public override RemovalOutcome Remove(RegistryBackup backup)
        {
            if (!Directory.Exists(Location))
            {
                return RemovalOutcome.Removed;
            }
            return FileEraser.DeleteFolder(Location) ? RemovalOutcome.RemovedAtRestart : RemovalOutcome.Removed;
        }
    }
}
