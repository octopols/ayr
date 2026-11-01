using System;

namespace Ayr.MsiRemoval.Components
{
    public sealed class ScheduledTask : MsiComponent
    {
        private static readonly TimeSpan DeleteTimeout = TimeSpan.FromMinutes(1);

        private readonly string _taskPath;

        public ScheduledTask(string taskPath, string program)
            : base(MsiComponentKind.ScheduledTask, taskPath.TrimStart('\\'), program)
        {
            _taskPath = taskPath;
        }

        public override RemovalOutcome Remove(RegistryBackup backup)
        {
            new ExternalCommand("schtasks.exe", "/Delete /TN \"" + _taskPath + "\" /F").RunOrThrow(DeleteTimeout);
            return RemovalOutcome.Removed;
        }
    }
}
