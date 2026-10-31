using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace Ayr.App
{
    /// <summary>
    /// The logon task that replaces what MSI's services did at boot. It runs the windowless Ayr.exe with highest
    /// privileges, so there is no UAC prompt and no console window.
    /// </summary>
    public sealed class AutostartTask
    {
        public const string TaskName = "Ayr";
        private const string TrayExecutable = "Ayr.exe";
        public const string SignInArgument = "startup";

        public bool IsInstalled => RunSchtasks("/query /tn " + TaskName) == 0;

        public void Install()
        {
            string executable = Path.Combine(AppPaths.ExecutableDirectory, TrayExecutable);
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException(TrayExecutable + " must sit next to ayrctl.exe.", executable);
            }
            string xmlPath = Path.Combine(Path.GetTempPath(), "ayrctl-task-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(xmlPath, TaskXml(executable), Encoding.Unicode);
                int exitCode = RunSchtasks("/create /tn " + TaskName + " /xml \"" + xmlPath + "\" /f");
                if (exitCode != 0)
                {
                    throw new InvalidOperationException("schtasks /create failed with exit code " + exitCode + ".");
                }
            }
            finally
            {
                File.Delete(xmlPath);
            }
            ActivityLog.Write("autostart task installed");
        }

        public void Remove()
        {
            if (IsInstalled)
            {
                RunSchtasks("/delete /tn " + TaskName + " /f");
            }
            ActivityLog.Write("autostart task removed");
        }

        private static string TaskXml(string executable)
        {
            string user = Xml(CurrentUser());
            return
"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
"<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
"  <RegistrationInfo><Description>Ayr: applies saved fan and performance settings at logon.</Description></RegistrationInfo>\r\n" +
"  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId><Delay>PT10S</Delay></LogonTrigger></Triggers>\r\n" +
"  <Principals><Principal id=\"Author\"><UserId>" + user + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
"  <Settings>\r\n" +
"    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n" +
"    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
"    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
"    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" +
"    <StartWhenAvailable>true</StartWhenAvailable>\r\n" +
"    <Priority>7</Priority>\r\n" +
"  </Settings>\r\n" +
"  <Actions Context=\"Author\"><Exec><Command>" + Xml(executable) + "</Command><Arguments>" + SignInArgument + "</Arguments>" +
"<WorkingDirectory>" + Xml(Path.GetDirectoryName(executable)) + "</WorkingDirectory></Exec></Actions>\r\n" +
"</Task>\r\n";
        }

        private static string CurrentUser()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return identity.Name;
            }
        }

        private static string Xml(string value) => SecurityElement.Escape(value);

        private static int RunSchtasks(string arguments)
        {
            var start = new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process schtasks = Process.Start(start))
            {
                schtasks.StandardOutput.ReadToEnd();
                schtasks.StandardError.ReadToEnd();
                schtasks.WaitForExit();
                return schtasks.ExitCode;
            }
        }
    }
}
