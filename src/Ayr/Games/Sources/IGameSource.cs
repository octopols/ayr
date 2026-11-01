using System.Collections.Generic;

namespace Ayr.Games.Sources
{
    /// <summary>One place games can be found, such as a launcher's install records.</summary>
    public interface IGameSource
    {
        /// <summary>Installed games only; damaged or stale records are skipped.</summary>
        IEnumerable<GameEntry> Find();
    }
}
