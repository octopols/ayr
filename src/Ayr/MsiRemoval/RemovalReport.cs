using System.Collections.Generic;
using System.Linq;

namespace Ayr.MsiRemoval
{
    public sealed class RemovalReport
    {
        public RemovalReport(IReadOnlyList<RemovalResult> results, RestorePointStatus restorePoint, string backupFolder)
        {
            Results = results;
            RestorePoint = restorePoint;
            BackupFolder = backupFolder;
        }

        public IReadOnlyList<RemovalResult> Results { get; }
        public RestorePointStatus RestorePoint { get; }
        public string BackupFolder { get; }

        public int FailedCount => Results.Count(result => result.Outcome == RemovalOutcome.Failed);
        public bool RestartNeeded => Results.Any(result => result.Outcome == RemovalOutcome.RemovedAtRestart);
    }
}
