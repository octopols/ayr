using System.Collections.Generic;

namespace Ayr.MsiRemoval.Components
{
    public interface IMsiComponentSource
    {
        IEnumerable<MsiComponent> Find();
    }
}
