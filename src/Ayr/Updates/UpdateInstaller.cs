using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ayr.App;

namespace Ayr.Updates
{
    /// <summary>
    /// Replaces the installed files with a release's. Files in use (the running executables, a loaded driver) can't be
    /// overwritten but can be renamed, so each changed file is moved aside to "*.old" and the new one copied into its
    /// place; the leftovers are deleted on the next start. If anything fails, every file is put back.
    /// </summary>
    public sealed class UpdateInstaller
    {
        private const string AsideExtension = ".old";
        private const string TrayExecutable = "Ayr.exe";
        private const string ConsoleExecutable = "ayrctl.exe";
        private const int MaxPackageBytes = 50 * 1024 * 1024;
        private const int MaxChecksumBytes = 1024;
        private static readonly char[] ChecksumSeparators = { ' ', '\t', '\r', '\n', '﻿' };

        private readonly WebDownloader _web = new WebDownloader();

        public void Install(ReleaseInfo release)
        {
            string workDirectory = Path.Combine(AppPaths.DataDirectory, "update");
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
            Directory.CreateDirectory(workDirectory);
            try
            {
                string package = Path.Combine(workDirectory, "package.zip");
                File.WriteAllBytes(package, DownloadVerified(release));
                string staged = Path.Combine(workDirectory, "files");
                ZipFile.ExtractToDirectory(package, staged);
                EnsureContains(staged, release.Version);
                Replace(staged, AppPaths.ExecutableDirectory);
            }
            finally
            {
                Directory.Delete(workDirectory, recursive: true);
            }
            ActivityLog.Write("installed Ayr " + release.Version);
        }

        /// <summary>Deletes files moved aside by an earlier update; ones still in use are left for the next start.</summary>
        public static void RemoveLeftovers()
        {
            foreach (string leftover in Directory.GetFiles(AppPaths.ExecutableDirectory, "*" + AsideExtension, SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(leftover);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Still mapped by a process or a loaded driver.
                }
            }
        }

        private byte[] DownloadVerified(ReleaseInfo release)
        {
            byte[] package = _web.Get(release.PackageUrl, MaxPackageBytes);
            string published = Encoding.UTF8.GetString(_web.Get(release.ChecksumUrl, MaxChecksumBytes))
                .Split(ChecksumSeparators, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.Equals(published, Sha256(package), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The downloaded update doesn't match its published checksum.");
            }
            return package;
        }

        private static void EnsureContains(string staged, Version expected)
        {
            string tray = Path.Combine(staged, TrayExecutable);
            if (!File.Exists(tray) || !File.Exists(Path.Combine(staged, ConsoleExecutable)))
            {
                throw new InvalidDataException("The update package is missing Ayr's executables.");
            }
            Version packaged = AppVersion.Normalize(AssemblyName.GetAssemblyName(tray).Version);
            if (packaged != expected)
            {
                throw new InvalidDataException("The update package contains version " + packaged + " instead of " + expected + ".");
            }
        }

        private static void Replace(string staged, string installDirectory)
        {
            var done = new List<ReplacedFile>();
            try
            {
                foreach (string source in Directory.GetFiles(staged, "*", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(installDirectory, source.Substring(staged.Length + 1));
                    if (File.Exists(target) && SameContent(source, target))
                    {
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    string aside = File.Exists(target) ? MoveAside(target) : null;
                    done.Add(new ReplacedFile(target, aside));
                    File.Copy(source, target);
                }
            }
            catch
            {
                for (int index = done.Count - 1; index >= 0; index--)
                {
                    done[index].Undo();
                }
                throw;
            }
        }

        private static string MoveAside(string target)
        {
            string aside = target + AsideExtension;
            if (File.Exists(aside))
            {
                try
                {
                    File.Delete(aside);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // An older copy is still in use; this one gets a name of its own.
                    aside = target + "." + Guid.NewGuid().ToString("N") + AsideExtension;
                }
            }
            File.Move(target, aside);
            return aside;
        }

        private static bool SameContent(string first, string second) =>
            new FileInfo(first).Length == new FileInfo(second).Length
            && File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(second));

        private static string Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
            }
        }

        private sealed class ReplacedFile
        {
            private readonly string _target;
            private readonly string _aside;

            public ReplacedFile(string target, string aside)
            {
                _target = target;
                _aside = aside;
            }

            public void Undo()
            {
                if (File.Exists(_target))
                {
                    File.Delete(_target);
                }
                if (_aside != null)
                {
                    File.Move(_aside, _target);
                }
            }
        }
    }
}
