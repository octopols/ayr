using System;
using System.Management;
using System.Runtime.InteropServices;
using Ayr.App;

namespace Ayr.MsiRemoval
{
    /// <summary>
    /// A Windows restore point, made only when the user asks for one. It is never required: System Restore is often
    /// turned off, and Windows silently skips a new point within 24 hours of the last one.
    /// </summary>
    public static class SystemRestorePoint
    {
        private const int ModifySettings = 12;
        private const int BeginSystemChange = 100;
        private const uint Success = 0;

        public static bool TryCreate(string description)
        {
            try
            {
                using (var restore = new ManagementClass(@"\\.\root\default", "SystemRestore", null))
                {
                    object result = restore.InvokeMethod("CreateRestorePoint", new object[] { description, ModifySettings, BeginSystemChange });
                    return Convert.ToUInt32(result) == Success;
                }
            }
            // With System Restore turned off, Windows answers with a bare COM error that has no message.
            catch (Exception ex) when (ex is ManagementException || ex is COMException)
            {
                ActivityLog.Write("restore point not created: " + ex.GetType().Name + " 0x" + ex.HResult.ToString("X8") + " " + ex.Message);
                return false;
            }
        }
    }
}
