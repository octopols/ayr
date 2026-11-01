using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ayr.App;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>MSI's folders that exist, except one Ayr itself was installed into.</summary>
    public sealed class MsiFolderSource : IMsiComponentSource
    {
        public IEnumerable<MsiComponent> Find() =>
            MicroStar.Folders
                .Where(Directory.Exists)
                .Where(folder => !FolderPath.Contains(folder, AppPaths.ExecutableDirectory))
                .Select(folder => new MsiFolder(folder));
    }
}
