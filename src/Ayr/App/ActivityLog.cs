using System;
using System.Globalization;
using System.IO;

namespace Ayr.App
{
    public static class ActivityLog
    {
        public static void Write(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine;
            try
            {
                File.AppendAllText(AppPaths.ActivityLog, line);
            }
            catch (IOException)
            {
                // A full disk or a locked log must never stop a thermal failsafe from running.
            }
        }
    }
}
