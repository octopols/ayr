using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;

namespace Ayr.App
{
    /// <summary>
    /// Hardware writes need administrator rights. Commands that need them relaunch ayrctl elevated and relay the child's
    /// output back into the original console through a temporary file.
    /// </summary>
    public static class Elevation
    {
        public const string RelayFlag = "--relay";
        private const int ErrorCancelled = 1223;

        public static bool IsElevated
        {
            get
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }

        /// <summary>Returns the elevated child's exit code, or null when the user declined the UAC prompt.</summary>
        public static int? RelaunchElevated(IEnumerable<string> args, TextWriter output)
        {
            string relayFile = Path.Combine(Path.GetTempPath(), "ayrctl-" + Guid.NewGuid().ToString("N") + ".txt");
            var start = new ProcessStartInfo(AppPaths.ExecutablePath, JoinArguments(args.Concat(new[] { RelayFlag, relayFile })))
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try
            {
                using (Process child = Process.Start(start))
                {
                    child.WaitForExit();
                    if (File.Exists(relayFile))
                    {
                        output.Write(File.ReadAllText(relayFile));
                        File.Delete(relayFile);
                    }
                    return child.ExitCode;
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
            {
                return null;
            }
        }

        public static void StartElevated(string arguments)
        {
            Process.Start(new ProcessStartInfo(AppPaths.ExecutablePath, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        private static string JoinArguments(IEnumerable<string> args) =>
            string.Join(" ", args.Select(arg => arg.Length == 0 || arg.Contains(' ') ? "\"" + arg + "\"" : arg));
    }
}
