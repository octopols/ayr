using System;
using System.Windows.Forms;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Hardware;

namespace Ayr.Ui
{
    /// <summary>Starts the tray: once per machine, elevated, and only once MSI's firmware interface is in place.</summary>
    public static class TrayLauncher
    {
        public const string TrayCommand = "tray";

        /// <summary>From the Start menu or "ayrctl tray"; tells the user if the tray is already running.</summary>
        public static void Launch() => Run(TrayCommand, announceIfRunning: true, restoreSettings: false);

        /// <summary>From the "Start with Windows" logon task: restores the saved settings first, and stays quiet if already running.</summary>
        public static void LaunchAtSignIn() => Run(AutostartTask.SignInArgument, announceIfRunning: false, restoreSettings: true);

        private static void Run(string command, bool announceIfRunning, bool restoreSettings)
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
                if (!FirmwareInterfaceReady())
                {
                    return;
                }
                if (restoreSettings)
                {
                    new StartupApplier(new HardwareContext()).ApplyOrFailsafe();
                }
                Application.Run(new TrayApp());
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
