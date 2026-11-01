using System;
using Ayr.MsiRemoval;

namespace Ayr.Ui
{
    /// <summary>What the MSI software page says about a component, a removal outcome and a finished removal.</summary>
    public static class MsiRemovalText
    {
        public static string Detail(MsiComponent component) => KindLabel(component.Kind) + " · " + component.Location;

        public static string Outcome(RemovalResult result)
        {
            switch (result.Outcome)
            {
                case RemovalOutcome.Removed:
                    return "Removed";
                case RemovalOutcome.RemovedAtRestart:
                    return "Removed. Finishes when Windows restarts";
                case RemovalOutcome.Failed:
                    return "Couldn't remove: " + result.Error;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown removal outcome.");
            }
        }

        public static string Summary(RemovalReport report)
        {
            int removed = report.Results.Count - report.FailedCount;
            string summary = "Removed " + removed + " of " + report.Results.Count + ".";
            if (report.FailedCount > 0)
            {
                summary += " " + report.FailedCount + " couldn't be removed.";
            }
            if (report.RestorePoint == RestorePointStatus.Failed)
            {
                summary += " No restore point: System Restore is off.";
            }
            if (report.RestartNeeded)
            {
                summary += " Restart Windows to finish.";
            }
            return summary;
        }

        private static string KindLabel(MsiComponentKind kind)
        {
            switch (kind)
            {
                case MsiComponentKind.DriverPackage:
                    return "Installs MSI apps through Windows Update";
                case MsiComponentKind.StoreApp:
                    return "Store app";
                case MsiComponentKind.Program:
                    return "Program";
                case MsiComponentKind.Service:
                    return "Service";
                case MsiComponentKind.Driver:
                    return "Driver";
                case MsiComponentKind.ScheduledTask:
                    return "Scheduled task";
                case MsiComponentKind.StartupEntry:
                    return "Starts with Windows";
                case MsiComponentKind.Settings:
                    return "Registry";
                case MsiComponentKind.Folder:
                    return "Folder";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown MSI component kind.");
            }
        }
    }
}
