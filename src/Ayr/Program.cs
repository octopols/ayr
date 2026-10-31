using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Cli;
using Ayr.Ui;

namespace Ayr
{
    public static class Program
    {
        private const string TrayCommand = "tray";

        [STAThread]
        public static int Main(string[] args)
        {
            // A UI bug must never put a .NET crash dialog over a game; it is logged instead.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => ActivityLog.Write("ui error: " + e.Exception);

            int relayAt = Array.IndexOf(args, Elevation.RelayFlag);
            bool relayed = relayAt >= 0 && relayAt + 1 < args.Length;
            using (TextWriter relay = relayed ? new StreamWriter(args[relayAt + 1], false, new UTF8Encoding(false)) { AutoFlush = true } : null)
            {
                TextWriter output = relay ?? IndependentConsole.CreateStdout();
                string[] commandArgs = relayed ? args.Take(relayAt).ToArray() : args;
                try
                {
                    return (int)Run(commandArgs, output);
                }
                catch (Exception ex)
                {
                    output.WriteLine("error: " + ex.Message);
                    ActivityLog.Write("command failed: " + string.Join(" ", commandArgs) + ": " + ex);
                    return (int)ExitCode.Failure;
                }
            }
        }

        private static ExitCode Run(string[] args, TextWriter output)
        {
            bool trayRequested = args.Length > 0 && args[0].Equals(TrayCommand, StringComparison.OrdinalIgnoreCase);
#if TRAY_HOST
            // Ayr.exe, the Start menu entry, opens the tray when started without arguments.
            trayRequested |= args.Length == 0;
#endif
            return trayRequested ? RunTray() : new CommandLine(output).Execute(args);
        }

        private static ExitCode RunTray()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (SingleInstance.IsRunning)
            {
                ShowAlreadyRunning();
                return ExitCode.Success;
            }
            if (!Elevation.IsElevated)
            {
                Elevation.StartElevated(TrayCommand);
                return ExitCode.Success;
            }
            using (SingleInstance instance = SingleInstance.TryAcquire())
            {
                if (instance == null)
                {
                    ShowAlreadyRunning();
                    return ExitCode.Success;
                }
                Application.Run(new TrayApp());
            }
            return ExitCode.Success;
        }

        private static void ShowAlreadyRunning() =>
            MessageBox.Show("Ayr is already running.\n\nClick the Ayr icon in the system tray.", "Ayr",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
