using System;
using System.Diagnostics;
using Ayr.App;
using Ayr.Battery;
using Ayr.Controllers;

namespace Ayr.Agent
{
    /// <summary>
    /// A separate process that outlives a crashed supervisor. When the supervisor ends for any reason, including being
    /// killed, fans left in Silent are put back on Auto and a battery calibration is ended.
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
            EndCalibration(supervisorProcessId);
        }

        // A calibration left running would keep the laptop on its battery with the charger plugged in, until it died.
        private void EndCalibration(int supervisorProcessId)
        {
            try
            {
                new BatteryCalibration(_hardware).Abandon("Battery calibration stopped: Ayr's background process ended unexpectedly.");
            }
            catch (Exception ex)
            {
                ActivityLog.Write("sentinel: ending the calibration after supervisor " + supervisorProcessId + " failed: " + ex.Message);
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
