using System;
using System.Collections.Generic;
using System.Linq;
using Ayr.App;
using Ayr.MsiRemoval.Components;

namespace Ayr.MsiRemoval
{
    /// <summary>Everything of MSI's on this PC, in removal order. A place that can't be read is logged and skipped.</summary>
    public sealed class MsiSoftwareScan
    {
        private readonly IReadOnlyList<IMsiComponentSource> _sources = new IMsiComponentSource[]
        {
            new DriverPackageSource(),
            new StoreAppSource(),
            new InstalledProgramSource(),
            new WindowsServiceSource(),
            new ScheduledTaskSource(),
            new StartupEntrySource(),
            new RegistrySettingsSource(),
            new MsiFolderSource()
        };

        public IReadOnlyList<MsiComponent> Run() =>
            _sources.SelectMany(FindIn).OrderBy(component => component.Kind).ThenBy(component => component.Name).ToList();

        private static IEnumerable<MsiComponent> FindIn(IMsiComponentSource source)
        {
            try
            {
                return source.Find().ToList();
            }
            catch (Exception ex)
            {
                ActivityLog.Write("MSI scan: " + source.GetType().Name + " failed: " + ex.Message);
                return Enumerable.Empty<MsiComponent>();
            }
        }
    }
}
