using System;
using System.IO;

namespace Ayr.MsiRemoval
{
    /// <summary>
    /// Splits a command line or a service's ImagePath into its program and arguments. These come in many shapes: quoted
    /// or not, with environment variables, or in the kernel's "\??\" and "System32\drivers\..." forms. A bare name such as
    /// "MsiExec.exe" is returned as is, for Windows to find on the PATH.
    /// </summary>
    public static class CommandPath
    {
        private const char Quote = '"';
        private const string KernelPrefix = @"\??\";
        private const string SystemRootPrefix = @"\SystemRoot\";
        private const string SystemFolderPrefix = @"System32\";
        private static readonly string[] ProgramExtensions = { ".exe", ".sys", ".dll" };

        /// <summary>Null when the command is empty.</summary>
        public static string ProgramOf(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }
            string expanded = Expand(command);
            return Absolute(expanded.Substring(0, ProgramEnd(expanded)).Trim(Quote));
        }

        public static string ArgumentsOf(string command)
        {
            string expanded = Expand(command);
            return expanded.Substring(ProgramEnd(expanded)).Trim();
        }

        private static string Expand(string command) => Environment.ExpandEnvironmentVariables(command.Trim());

        // Unquoted paths may contain spaces, so an unquoted program ends at its extension rather than at the first space.
        private static int ProgramEnd(string command)
        {
            if (command.Length > 0 && command[0] == Quote)
            {
                int closing = command.IndexOf(Quote, 1);
                return closing < 0 ? command.Length : closing + 1;
            }
            foreach (string extension in ProgramExtensions)
            {
                int index = command.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
                int end = index + extension.Length;
                if (index > 0 && (end == command.Length || command[end] == ' '))
                {
                    return end;
                }
            }
            int space = command.IndexOf(' ');
            return space < 0 ? command.Length : space;
        }

        private static string Absolute(string program)
        {
            if (program.StartsWith(KernelPrefix))
            {
                return program.Substring(KernelPrefix.Length);
            }
            if (program.StartsWith(SystemRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(WindowsDirectory, program.Substring(SystemRootPrefix.Length));
            }
            if (program.StartsWith(SystemFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(WindowsDirectory, program);
            }
            return program;
        }

        private static string WindowsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    }
}
