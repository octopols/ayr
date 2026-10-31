using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Ayr.App;

namespace Ayr.Apple
{
    /// <summary>
    /// MSI Super Charger's fast-charge request, made the way Super Charger's own service made it: by running MSI's unmodified
    /// ipadchg2.exe from Super Charger's folder. ipadchg2 starts the ipadtst2 driver if needed and sends the request
    /// (FINDINGS-SuperCharger.md).
    ///
    /// Learned the hard way on this PC (2026-10-31): a burst of requests hung it and stopping the driver bugchecked it
    /// (0x139/3). So ayrctl never stops the driver (it stays loaded until reboot, as with Super Charger), and requests are
    /// serialized system-wide and spaced at least MinimumGap apart, Super Charger's own cadence.
    /// </summary>
    public sealed class AppleChargeDriver
    {
        public static readonly TimeSpan MinimumGap = TimeSpan.FromMinutes(5);

        private const string DriverServiceName = "ipadtst2";
        private const string HelperName = "ipadchg2.exe";
        private const string RequestMutexName = "Global\\Ayr.AppleChargeSend";
        private static readonly TimeSpan HelperTimeout = TimeSpan.FromSeconds(30);

        // The hub drivers the request inside ipadtst2_64.sys is written for.
        private static readonly string[] SupportedHubServices = { "USBHUB3", "amdhub31" };

        private static string SuperChargerDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MSI", "Super Charger");

        private static string HelperPath => Path.Combine(SuperChargerDirectory, HelperName);

        public static bool SupportsHub(string hubService) =>
            Array.Exists(SupportedHubServices, supported => string.Equals(supported, hubService, StringComparison.OrdinalIgnoreCase));

        public bool IsInstalled => DriverStatus().HasValue && File.Exists(HelperPath);

        public bool IsLoaded => DriverStatus() == ServiceControllerStatus.Running;

        /// <summary>Runs ipadchg2.exe once, unless a request is already in progress or the last one was too recent.</summary>
        public bool TrySend(AppleRequestLog log, out string refusal)
        {
            using (var mutex = new Mutex(false, RequestMutexName))
            {
                if (!TryAcquire(mutex))
                {
                    refusal = "another request is being sent right now";
                    return false;
                }
                try
                {
                    TimeSpan sinceLast = DateTime.Now - (log.LastRequest ?? DateTime.MinValue);
                    if (sinceLast < MinimumGap)
                    {
                        refusal = string.Format("last request was {0:0} s ago (minimum gap {1:0} min, like Super Charger)",
                            sinceLast.TotalSeconds, MinimumGap.TotalMinutes);
                        return false;
                    }
                    // Recorded before running: if the request takes the machine down, the next attempt still honours the gap.
                    log.RecordRequest(DateTime.Now);
                    RunHelper();
                    refusal = null;
                    return true;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static bool TryAcquire(Mutex mutex)
        {
            try
            {
                return mutex.WaitOne(TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }

        private static void RunHelper()
        {
            if (!File.Exists(HelperPath))
            {
                throw new FileNotFoundException("MSI Super Charger's " + HelperName + " is missing.", HelperPath);
            }
            // ipadchg2 builds the driver path from its current directory.
            var start = new ProcessStartInfo(HelperPath)
            {
                WorkingDirectory = SuperChargerDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var output = new StringBuilder();
            using (var helper = new Process { StartInfo = start })
            {
                DataReceivedEventHandler collect = (sender, line) =>
                {
                    if (line.Data == null)
                    {
                        return;
                    }
                    lock (output)
                    {
                        output.Append(line.Data.Trim()).Append(' ');
                    }
                };
                helper.OutputDataReceived += collect;
                helper.ErrorDataReceived += collect;
                helper.Start();
                helper.BeginOutputReadLine();
                helper.BeginErrorReadLine();
                if (!helper.WaitForExit((int)HelperTimeout.TotalMilliseconds))
                {
                    throw new System.TimeoutException(HelperName + " did not finish within " + HelperTimeout.TotalSeconds + " s.");
                }
                helper.WaitForExit();
                ActivityLog.Write("ipadchg2 exit " + helper.ExitCode + ": " + output.ToString().Trim());
            }
        }

        private static ServiceControllerStatus? DriverStatus()
        {
            ServiceController[] drivers = ServiceController.GetDevices();
            try
            {
                ServiceController driver = Array.Find(drivers, candidate =>
                    string.Equals(candidate.ServiceName, DriverServiceName, StringComparison.OrdinalIgnoreCase));
                return driver?.Status;
            }
            finally
            {
                foreach (ServiceController controller in drivers)
                {
                    controller.Dispose();
                }
            }
        }
    }
}
