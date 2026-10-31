using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Ayr.App;
using Ayr.Hardware;

namespace Ayr.Apple
{
    /// <summary>
    /// MSI Super Charger's fast-charge request, sent with MSI's own unmodified helper (ipadchg2.exe) and kernel driver
    /// (ipadtst2_64.sys), which Ayr copies into vendor\ so Super Charger itself isn't needed (docs/apple-fast-charge.md).
    /// Ayr starts the driver and locks it down before the helper runs; the helper then only sends the request.
    ///
    /// On Windows 11 25H2 a burst of requests hung the PC and stopping the driver bugchecked it (0x139/3). So the driver is
    /// never stopped (it stays loaded until restart, as with Super Charger), and requests are serialized system-wide and
    /// spaced at least MinimumGap apart, Super Charger's own cadence.
    /// </summary>
    public sealed class AppleChargeDriver
    {
        public static readonly TimeSpan MinimumGap = TimeSpan.FromMinutes(5);

        private const string RequestMutexName = "Global\\Ayr.AppleChargeSend";
        private static readonly TimeSpan HelperTimeout = TimeSpan.FromSeconds(30);

        // The hub drivers the request inside ipadtst2_64.sys is written for.
        private static readonly string[] SupportedHubServices = { "USBHUB3", "amdhub31" };

        public static bool SupportsHub(string hubService) =>
            Array.Exists(SupportedHubServices, supported => string.Equals(supported, hubService, StringComparison.OrdinalIgnoreCase));

        /// <summary>True when Ayr has, or can still adopt, both of MSI's files.</summary>
        public bool IsAvailable => VendorLibrary.AppleChargeHelper.IsAvailable && VendorLibrary.AppleChargeDriver.IsAvailable;

        public bool IsLoaded => KernelDriver.AppleCharge.IsRunning;

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
            string helperPath = VendorLibrary.AppleChargeHelper.Locate();
            KernelDriver.AppleCharge.EnsureRunning();
            // ipadchg2 would install the driver from its current directory if it found the device missing.
            var start = new ProcessStartInfo(helperPath)
            {
                WorkingDirectory = Path.GetDirectoryName(helperPath),
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
                    throw new TimeoutException("ipadchg2.exe did not finish within " + HelperTimeout.TotalSeconds + " s.");
                }
                helper.WaitForExit();
                ActivityLog.Write("ipadchg2 exit " + helper.ExitCode + ": " + output.ToString().Trim());
            }
        }
    }
}
