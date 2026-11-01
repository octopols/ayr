using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Ayr.App;
using Ayr.Games;
using Ayr.Hardware;

namespace Ayr.MsiRemoval
{
    /// <summary>
    /// Removes the chosen MSI components. Before anything is touched, Ayr's own driver and WMI registrations are pointed
    /// at Ayr's copies, so removing MSI's files can't take away what Ayr needs; if that fails, nothing is removed. Then a
    /// restore point is made if asked for, MSI's running programs are closed, and the components go in order, each backed
    /// up first. Neither a missing restore point nor one component failing stops the rest.
    /// </summary>
    public sealed class MsiRemover
    {
        private const string RestorePointDescription = "Before removing MSI software (Ayr)";
        private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);

        public RemovalReport Remove(IReadOnlyList<MsiComponent> components, bool makeRestorePoint, IRemovalProgress progress)
        {
            KeepAyrDependencies();
            RestorePointStatus restorePoint = makeRestorePoint ? MakeRestorePoint() : RestorePointStatus.NotRequested;
            CloseMsiPrograms();
            var backup = new RegistryBackup();
            var results = new List<RemovalResult>();
            foreach (MsiComponent component in components.OrderBy(component => component.Kind))
            {
                progress.Starting(component);
                RemovalResult result = RemoveOne(component, backup);
                results.Add(result);
                progress.Finished(result);
            }
            return new RemovalReport(results, restorePoint, backup.Folder);
        }

        private static RestorePointStatus MakeRestorePoint() =>
            SystemRestorePoint.TryCreate(RestorePointDescription) ? RestorePointStatus.Created : RestorePointStatus.Failed;

        private static void KeepAyrDependencies()
        {
            try
            {
                KernelDriver.EcAccess.PointAtOwnCopy();
                KernelDriver.AppleCharge.PointAtOwnCopy();
                FirmwareInterface.PointAtOwnCopy();
            }
            catch (Exception ex)
            {
                ActivityLog.Write("MSI removal stopped before it began: " + ex);
                throw new InvalidOperationException("Couldn't prepare the removal: " + ExceptionText.Of(ex), ex);
            }
        }

        private static RemovalResult RemoveOne(MsiComponent component, RegistryBackup backup)
        {
            try
            {
                RemovalCompletion completion = component.Remove(backup);
                ActivityLog.Write("MSI removal: " + component.Kind + " " + component.Name + " (" + component.Location + "): removed " + completion);
                return RemovalResult.Succeeded(component, completion);
            }
            catch (Exception ex)
            {
                ActivityLog.Write("MSI removal: " + component.Kind + " " + component.Name + " failed: " + ex);
                return RemovalResult.Failed(component, ExceptionText.Of(ex));
            }
        }

        // Programs that are running hold their files and may undo changes, so they are closed first.
        private static void CloseMsiPrograms()
        {
            foreach (KeyValuePair<int, string> process in ProcessPaths.All().Where(process => MicroStar.OwnsFile(process.Value)))
            {
                try
                {
                    using (Process running = Process.GetProcessById(process.Key))
                    {
                        running.Kill();
                        running.WaitForExit((int)ExitTimeout.TotalMilliseconds);
                    }
                    ActivityLog.Write("MSI removal: closed " + process.Value);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Win32Exception)
                {
                    ActivityLog.Write("MSI removal: could not close " + process.Value + ": " + ex.Message);
                }
            }
        }
    }
}
