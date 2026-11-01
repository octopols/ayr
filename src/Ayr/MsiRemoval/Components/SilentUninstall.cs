using System;
using System.IO;
using System.Text.RegularExpressions;
using Ayr.App;

namespace Ayr.MsiRemoval.Components
{
    /// <summary>
    /// Turns a program's registered uninstall command into one that runs without questions. Windows Installer products
    /// and Inno Setup uninstallers (MSI uses both) have known silent switches; anything else runs as registered and may
    /// show its own window.
    /// </summary>
    public static class SilentUninstall
    {
        private static readonly Regex ProductCode = new Regex(@"\{[0-9A-Fa-f\-]{36}\}");
        private static readonly Regex InnoSetupUninstaller = new Regex(@"^unins\d{3}\.exe$", RegexOptions.IgnoreCase);

        /// <summary>Null when the program registered no uninstall command.</summary>
        public static ExternalCommand For(string quietUninstallString, string uninstallString)
        {
            if (!string.IsNullOrWhiteSpace(quietUninstallString))
            {
                return Registered(quietUninstallString, "");
            }
            if (string.IsNullOrWhiteSpace(uninstallString))
            {
                return null;
            }
            Match productCode = ProductCode.Match(uninstallString);
            if (uninstallString.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0 && productCode.Success)
            {
                return new ExternalCommand("msiexec.exe", "/x " + productCode.Value + " /qn /norestart");
            }
            string program = CommandPath.ProgramOf(uninstallString);
            bool innoSetup = InnoSetupUninstaller.IsMatch(Path.GetFileName(program));
            return Registered(uninstallString, innoSetup ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" : "");
        }

        private static ExternalCommand Registered(string command, string extraArguments) =>
            new ExternalCommand(CommandPath.ProgramOf(command), (CommandPath.ArgumentsOf(command) + " " + extraArguments).Trim());
    }
}
