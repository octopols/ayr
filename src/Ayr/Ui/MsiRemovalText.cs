using System;
using System.Collections.Generic;
using System.Linq;
using Ayr.MsiRemoval;

namespace Ayr.Ui
{
    /// <summary>What the MSI software page says about a component, a removal outcome and a finished removal.</summary>
    public static class MsiRemovalText
    {
        public const string Scanning = "Looking for MSI software…";
        public const string ScanningAfterRemoval = "MSI software is still being removed from earlier. The list appears when that's done.";
        public const string ScanFailed = "Couldn't look for MSI software. Scan again, or restart Ayr and try again.";
        public const string NothingSelected = "Switch on what you want to remove.";
        public const string PrepareFailed = "Nothing was removed. Restart Ayr and try again.";
        public const string Queued = "In line to be removed";
        public const string Removing = "Removing…";

        private const string ScanIncomplete = " Some places couldn't be checked, so this list may be incomplete.";

        public static string Detail(MsiComponent component) => KindLabel(component.Kind) + " · " + component.Location;

        public static string Found(int count, bool incomplete) => Items(count) + " found." + (incomplete ? ScanIncomplete : "");

        public static string Confirmation(IReadOnlyCollection<MsiComponent> selected)
        {
            string confirmation = "Removes " + Items(selected.Count) + ". This can't be undone; MSI's settings are saved to a backup folder first.";
            int drivers = selected.Count(component => component.Kind == MsiComponentKind.Driver);
            if (drivers > 0)
            {
                confirmation += " " + (drivers == 1 ? "1 driver finishes" : drivers + " drivers finish") + " removing when you restart Windows.";
            }
            return confirmation;
        }

        public static string Progress(int started, int total, MsiComponent component) =>
            "Removing " + started + " of " + total + ": " + component.Name + ". If an MSI uninstall window opens, finish it there.";

        public static string Outcome(RemovalResult result)
        {
            switch (result.Outcome)
            {
                case RemovalOutcome.Removed:
                    return "Removed";
                case RemovalOutcome.RemovedAtRestart:
                    return "Removed. Finishes when Windows restarts";
                case RemovalOutcome.Failed:
                    return "Couldn't remove. Restart Windows, then scan again and retry";
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown removal outcome.");
            }
        }

        public static string Summary(RemovalReport report)
        {
            int removed = report.Results.Count - report.FailedCount;
            string summary = report.RestartNeeded ? "Restart Windows to finish removing. " : "";
            summary += "Removed " + removed + " of " + report.Results.Count + ".";
            if (report.FailedCount > 0)
            {
                summary += " " + report.FailedCount + " couldn't be removed; scan again to retry them.";
            }
            if (report.RestorePoint == RestorePointStatus.Failed)
            {
                summary += " Windows didn't make a restore point, so removal went ahead without one.";
            }
            return summary;
        }

        private static string Items(int count) => count == 1 ? "1 item" : count + " items";

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
                    return "Settings";
                case MsiComponentKind.Folder:
                    return "Folder";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown MSI component kind.");
            }
        }
    }
}
