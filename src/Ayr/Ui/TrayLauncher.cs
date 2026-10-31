using System.Windows.Forms;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Hardware;

namespace Ayr.Ui
{
    /// <summary>Starts the tray: once per machine, elevated, with MSI's files copied into Ayr's own folder first.</summary>
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
                VendorLibrary.AdoptAllAvailable();
                if (restoreSettings)
                {
                    new StartupApplier(new HardwareContext()).ApplyOrFailsafe();
                }
                Application.Run(new TrayApp());
            }
        }

        private static void ShowAlreadyRunning() =>
            MessageBox.Show("Ayr is already running.\n\nClick the Ayr icon in the system tray.", "Ayr",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
