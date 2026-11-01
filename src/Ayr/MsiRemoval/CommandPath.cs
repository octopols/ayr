using System;
using System.IO;

namespace Ayr.MsiRemoval
{
    /// <summary>
    /// The program a command line or a service's ImagePath runs. These come in many shapes: quoted or not, with
    /// arguments, with environment variables, or in the kernel's "\??\" and "System32\drivers\..." forms. A bare name such
    /// as "MsiExec.exe" is returned as is, for Windows to find on the PATH.
    /// </summary>
    public static class CommandPath
    {
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
            string expanded = Environment.ExpandEnvironmentVariables(command.Trim());
            string program = expanded.StartsWith("\"") ? Quoted(expanded) : Unquoted(expanded);
            return Absolute(program);
        }

        /// <summary>What follows the program: the arguments, without surrounding spaces.</summary>
        public static string ArgumentsOf(string command)
        {
            string expanded = Environment.ExpandEnvironmentVariables(command.Trim());
            int end = expanded.StartsWith("\"") ? expanded.IndexOf('"', 1) + 1 : Unquoted(expanded).Length;
            return end <= 0 || end >= expanded.Length ? "" : expanded.Substring(end).Trim();
        }

        private static string Quoted(string command)
        {
            int closing = command.IndexOf('"', 1);
            return closing < 0 ? command.Substring(1) : command.Substring(1, closing - 1);
        }

        // Unquoted paths may contain spaces, so the program ends at its extension rather than at the first space.
        private static string Unquoted(string command)
        {
            foreach (string extension in ProgramExtensions)
            {
                int index = command.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
                int end = index + extension.Length;
                if (index > 0 && (end == command.Length || command[end] == ' '))
                {
                    return command.Substring(0, end);
                }
            }
            int space = command.IndexOf(' ');
            return space < 0 ? command : command.Substring(0, space);
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
