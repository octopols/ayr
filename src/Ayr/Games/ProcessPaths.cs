using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Ayr.Interop;
using Microsoft.Win32.SafeHandles;

namespace Ayr.Games
{
    /// <summary>Executable paths of running processes, read with the least access Windows offers (works on most protected processes).</summary>
    public static class ProcessPaths
    {
        private const int MaxPathLength = 32767;

        /// <summary>Null when the process has exited or won't say.</summary>
        public static string Of(int processId)
        {
            using (SafeProcessHandle process = Kernel32.OpenProcess(Kernel32.ProcessQueryLimitedInformation, false, processId))
            {
                if (process.IsInvalid)
                {
                    return null;
                }
                var path = new StringBuilder(MaxPathLength);
                int length = path.Capacity;
                return Kernel32.QueryFullProcessImageName(process, 0, path, ref length) ? path.ToString(0, length) : null;
            }
        }

        public static Dictionary<int, string> All()
        {
            var paths = new Dictionary<int, string>();
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    string path = Of(process.Id);
                    if (path != null)
                    {
                        paths[process.Id] = path;
                    }
                }
            }
            return paths;
        }
    }
}
