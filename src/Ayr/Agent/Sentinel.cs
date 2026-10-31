using System;
using System.Diagnostics;
using Ayr.App;
using Ayr.Controllers;

namespace Ayr.Agent
{
    /// <summary>
    /// A separate process that outlives a crashed supervisor. When the supervisor ends for any reason, including being
    /// killed, fans left in Silent are put back on Auto.
    /// </summary>
    public sealed class Sentinel
    {
        private readonly HardwareContext _hardware;

        public Sentinel(HardwareContext hardware)
        {
            _hardware = hardware;
        }

        public void Watch(int supervisorProcessId)
        {
            WaitForExit(supervisorProcessId);
            if (ReadModeOrAssumeSilent() == FanMode.Silent)
            {
                new ModeService(_hardware).FailsafeAuto("sentinel: supervisor " + supervisorProcessId + " ended while Silent");
            }
        }

        private static void WaitForExit(int processId)
        {
            Process supervisor;
            try
            {
                supervisor = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return;
            }
            // This process may wait for days; it gives its memory back first.
            WorkingSet.Trim();
            using (supervisor)
            {
                supervisor.WaitForExit();
            }
        }

        private FanMode ReadModeOrAssumeSilent()
        {
            try
            {
                return _hardware.Fans.ReadMode();
            }
            catch (Exception ex)
            {
                ActivityLog.Write("sentinel could not read fan mode (" + ex.Message + "); forcing Auto");
                return FanMode.Silent;
            }
        }
    }
}
