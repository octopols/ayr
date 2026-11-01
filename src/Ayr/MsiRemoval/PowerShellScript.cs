using System;

namespace Ayr.MsiRemoval
{
    /// <summary>
    /// A short Windows PowerShell script, for the Store package commands that have no plain API in .NET Framework.
    /// Errors stop the script, so a failure surfaces as a non-zero exit code with PowerShell's own message.
    /// </summary>
    public sealed class PowerShellScript
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

        private readonly string _script;

        public PowerShellScript(string script)
        {
            _script = "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'; " + script;
        }

        public string Run()
        {
            var command = new ExternalCommand("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + _script.Replace("\"", "\\\"") + "\"");
            int exitCode = command.Run(Timeout, out string output);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(output.Length > 0 ? output : "PowerShell failed with exit code " + exitCode + ".");
            }
            return output;
        }

        /// <summary>Quotes a value for use inside the script.</summary>
        public static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    }
}
