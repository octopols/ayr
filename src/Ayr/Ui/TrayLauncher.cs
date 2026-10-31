using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Hardware;
using Ayr.Updates;

namespace Ayr.Ui
{
    /// <summary>Starts the tray: once per machine, elevated, and only once MSI's firmware interface is in place.</summary>
    public static class TrayLauncher
    {
        public const string TrayCommand = "tray";
        public const string AfterUpdateCommand = "after-update";

        // The old tray's sentinel checks the fan mode once that tray has exited. If the new tray had already restored
        // Silent by then, the sentinel would take it for an unguarded Silent and switch the fans to Auto.
        private static readonly TimeSpan SentinelGrace = TimeSpan.FromSeconds(3);

        /// <summary>From the Start menu or "ayrctl tray"; tells the user if the tray is already running.</summary>
        public static void Launch() => Run(TrayCommand, announceIfRunning: true, restoreSettings: false, startupNotice: null);

        /// <summary>From the "Start with Windows" logon task: restores the saved settings first, and stays quiet if already running.</summary>
        public static void LaunchAtSignIn() => Run(AutostartTask.SignInArgument, announceIfRunning: false, restoreSettings: true, startupNotice: null);

        /// <summary>Called by the old tray right before it exits, once the new version's files are in place.</summary>
        public static void StartAfterUpdate()
        {
            string arguments = AfterUpdateCommand + " " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
            Process.Start(new ProcessStartInfo(AppPaths.ExecutablePath, arguments) { UseShellExecute = true }).Dispose();
        }

        /// <summary>The new version, started by StartAfterUpdate: waits for the old tray to exit, then takes over with the saved settings.</summary>
        public static void LaunchAfterUpdate(IReadOnlyList<string> args)
        {
            if (args.Count < 2 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int previousTray))
            {
                throw new ArgumentException("usage: " + AfterUpdateCommand + " <process id of the previous tray>");
            }
            WaitForExit(previousTray);
            Thread.Sleep(SentinelGrace);
            Run(AfterUpdateCommand, announceIfRunning: false, restoreSettings: true, startupNotice: "Ayr was updated to version " + AppVersion.Current + ".");
        }

        private static void WaitForExit(int processId)
        {
            try
            {
                using (Process previous = Process.GetProcessById(processId))
                {
                    previous.WaitForExit();
                }
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }

        private static void Run(string command, bool announceIfRunning, bool restoreSettings, string startupNotice)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (SingleInstance.IsRunning)
            {
                if (announceIfRunning)
                {
                    ShowAlreadyRunning();
                }
                return;
            }
            if (!Elevation.IsElevated)
            {
                Elevation.StartElevated(command);
                return;
            }
            using (SingleInstance instance = SingleInstance.TryAcquire())
            {
                if (instance == null)
                {
                    if (announceIfRunning)
                    {
                        ShowAlreadyRunning();
                    }
                    return;
                }
                UpdateInstaller.RemoveLeftovers();
                if (!FirmwareInterfaceReady())
                {
                    return;
                }
                if (restoreSettings)
                {
                    new StartupApplier(new HardwareContext()).ApplyOrFailsafe();
                }
                Application.Run(new TrayApp(startupNotice));
            }
        }

        private static bool FirmwareInterfaceReady()
        {
            switch (FirmwareInterface.EnsureRegistered())
            {
                case FirmwareInterfaceState.Ready:
                    return true;
                case FirmwareInterfaceState.RestartRequired:
                    ShowMessage("Ayr has installed MSI's firmware interface. Restart Windows to finish setting it up.", MessageBoxIcon.Information);
                    return false;
                case FirmwareInterfaceState.NotAnMsiLaptop:
                    ShowMessage("Ayr only works on MSI laptops: this PC doesn't report MSI as its maker.", MessageBoxIcon.Warning);
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(FirmwareInterfaceState));
            }
        }

        private static void ShowAlreadyRunning() =>
            ShowMessage("Ayr is already running.\n\nClick the Ayr icon in the system tray.", MessageBoxIcon.Information);

        private static void ShowMessage(string text, MessageBoxIcon icon) => MessageBox.Show(text, "Ayr", MessageBoxButtons.OK, icon);
    }
}
