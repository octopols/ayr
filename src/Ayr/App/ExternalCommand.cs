using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Ayr.App
{
    /// <summary>A program run to completion with its output captured, such as reg.exe, schtasks.exe or an uninstaller.</summary>
    public sealed class ExternalCommand
    {
        // A child the program started can inherit the output pipe and keep it open after the program itself has exited.
        private static readonly TimeSpan OutputGrace = TimeSpan.FromSeconds(5);

        private readonly string _program;
        private readonly string _arguments;

        public ExternalCommand(string program, string arguments)
        {
            _program = program;
            _arguments = arguments;
        }

        public override string ToString() => _program + " " + _arguments;

        /// <summary>Returns the exit code; throws if the program does not finish in time, leaving it running.</summary>
        public int Run(TimeSpan timeout, out string output)
        {
            var start = new ProcessStartInfo(_program, _arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(start))
            {
                Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
                Task<string> standardError = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    throw new TimeoutException(_program + " did not finish within " + timeout.TotalMinutes + " minutes.");
                }
                bool outputComplete = Task.WaitAll(new Task[] { standardOutput, standardError }, OutputGrace);
                output = outputComplete ? (standardOutput.Result + standardError.Result).Trim() : "";
                return process.ExitCode;
            }
        }

        /// <summary>Like Run, but a non-zero exit code is an error carrying the program's own message.</summary>
        public void RunOrThrow(TimeSpan timeout)
        {
            int exitCode = Run(timeout, out string output);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(output.Length > 0 ? output : _program + " failed with exit code " + exitCode + ".");
            }
        }
    }
}
