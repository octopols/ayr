using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ayr.MsiRemoval;

namespace Ayr.Cli
{
    /// <summary>"ayrctl msi ...": the MSI software on this PC, and removing it.</summary>
    public sealed class MsiCommands
    {
        public const string Usage =
@"ayrctl msi                           list MSI's apps, services, drivers, tasks, settings and folders on this PC
ayrctl msi remove --yes [--restore-point]
                                     remove everything listed except Afterburner and Kombustor";

        private const string ConfirmFlag = "--yes";
        private const string RestorePointFlag = "--restore-point";

        private sealed class ConsoleProgress : IRemovalProgress
        {
            private readonly TextWriter _output;

            public ConsoleProgress(TextWriter output)
            {
                _output = output;
            }

            public void Starting(MsiComponent component) => _output.WriteLine("Removing {0} {1}…", component.Kind, component.Name);

            public void Finished(RemovalResult result) =>
                _output.WriteLine("  {0}{1}", result.Outcome, result.Error == null ? "" : ": " + result.Error);
        }

        private readonly TextWriter _output;

        public MsiCommands(TextWriter output)
        {
            _output = output;
        }

        public ExitCode Execute(IReadOnlyList<string> arguments)
        {
            IReadOnlyList<MsiComponent> components = new MsiSoftwareScan().Run().Components;
            string subcommand = arguments.FirstOrDefault();
            if (subcommand == null)
            {
                List(components);
                return ExitCode.Success;
            }
            if (subcommand == "remove" && arguments.Contains(ConfirmFlag))
            {
                return Remove(components.Where(component => component.SelectedByDefault).ToList(), arguments.Contains(RestorePointFlag));
            }
            _output.WriteLine(Usage);
            return ExitCode.Usage;
        }

        private void List(IReadOnlyList<MsiComponent> components)
        {
            if (components.Count == 0)
            {
                _output.WriteLine("No MSI software found.");
                return;
            }
            foreach (MsiComponent component in components)
            {
                _output.WriteLine("{0,-14} {1}{2}  {3}", component.Kind, component.Name, component.SelectedByDefault ? "" : " (kept unless chosen)", component.Location);
            }
        }

        private ExitCode Remove(IReadOnlyList<MsiComponent> components, bool makeRestorePoint)
        {
            if (components.Count == 0)
            {
                _output.WriteLine("No MSI software found.");
                return ExitCode.Success;
            }
            RemovalReport report = new MsiRemover().Remove(components, makeRestorePoint, new ConsoleProgress(_output));
            _output.WriteLine("Restore point: " + report.RestorePoint);
            _output.WriteLine("Registry backup: " + report.BackupFolder);
            if (report.RestartNeeded)
            {
                _output.WriteLine("Restart Windows to finish.");
            }
            return report.FailedCount == 0 ? ExitCode.Success : ExitCode.Failure;
        }
    }
}
