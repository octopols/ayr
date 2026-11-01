using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Ayr.Interop;

namespace Ayr.MsiRemoval
{
    /// <summary>
    /// Deletes files and folders now, or at the next restart when something has them open (a loaded driver, a running
    /// service). Windows performs restart deletions in the order they were registered, so files go before their folders.
    /// </summary>
    public static class FileEraser
    {
        /// <summary>Returns true when part of the folder is only deleted at the next restart.</summary>
        public static bool DeleteFolder(string folder)
        {
            List<string> files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToList();
            List<string> folders = Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
                .Concat(new[] { folder })
                .ToList();
            bool deferred = false;
            foreach (string file in files)
            {
                deferred |= DeleteFile(file);
            }
            foreach (string path in folders)
            {
                deferred |= Delete(path, () => Directory.Delete(path));
            }
            return deferred;
        }

        /// <summary>Returns true when the file is only deleted at the next restart.</summary>
        public static bool DeleteFile(string file) =>
            Delete(file, () =>
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            });

        private static bool Delete(string path, Action delete)
        {
            try
            {
                delete();
                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (!Kernel32.MoveFileEx(path, null, Kernel32.MoveFileDelayUntilReboot))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not delete " + path + ", not even at restart.");
                }
                return true;
            }
        }
    }
}
