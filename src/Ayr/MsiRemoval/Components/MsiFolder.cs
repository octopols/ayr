using System.IO;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>One of MSI's top-level folders, with everything its programs left in it.</summary>
    public sealed class MsiFolder : MsiComponent
    {
        private readonly string _path;

        public MsiFolder(string path) : base(MsiComponentKind.Folder, "MSI files", path)
        {
            _path = path;
        }

        public override RemovalCompletion Remove(RegistryBackup backup)
        {
            if (!Directory.Exists(_path))
            {
                return RemovalCompletion.Now;
            }
            return FileEraser.DeleteFolder(_path) ? RemovalCompletion.AtRestart : RemovalCompletion.Now;
        }
    }
}
