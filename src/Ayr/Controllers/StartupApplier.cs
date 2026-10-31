using System;
using System.Collections.Generic;
using Ayr.App;
using Ayr.Hardware;

namespace Ayr.Controllers
{
    /// <summary>
    /// What MSI's services did at boot: the firmware handshake MSI Foundation Service performed on start, then the user's
    /// saved fan mode, performance level and Turbo GPU offsets.
    /// </summary>
    public sealed class StartupApplier
    {
        private readonly HardwareContext _hardware;

        public StartupApplier(HardwareContext hardware)
        {
            _hardware = hardware;
        }

        /// <summary>Applies the saved settings; if that fails, the fans are put on Auto rather than left half-configured.</summary>
        public void ApplyOrFailsafe()
        {
            try
            {
                Apply();
            }
            catch (Exception ex)
            {
                ActivityLog.Write("startup apply failed: " + ex);
                new ModeService(_hardware).FailsafeAuto("startup apply failed");
            }
        }

        /// <summary>Returns one line per action taken.</summary>
        public IReadOnlyList<string> Apply()
        {
            var report = new List<string> { Handshake() };
            DesiredState desired = DesiredState.Load();
            var modes = new ModeService(_hardware);
            if (desired.Fan != FanMode.Custom)
            {
                modes.ApplyFan(desired.Fan);
                report.Add("fan " + desired.Fan.ToString().ToLowerInvariant());
            }
            if (desired.Performance != PerformanceLevel.Unknown)
            {
                modes.ApplyPerformance(desired.Performance);
                string offsets = desired.Performance == PerformanceLevel.Turbo ? " (" + desired.TurboOffsets + ")" : "";
                report.Add("performance " + desired.Performance.ToString().ToLowerInvariant() + offsets);
            }
            ActivityLog.Write("apply: " + string.Join("; ", report));
            return report;
        }

        private string Handshake()
        {
            byte current = _hardware.Wmi.ReadByte(WmiBlock.Software, RegisterMap.HandshakeIndex);
            if (current == RegisterMap.HandshakeValue)
            {
                return "firmware handshake already set";
            }
            _hardware.Wmi.WriteByte(WmiBlock.Software, RegisterMap.HandshakeIndex, RegisterMap.HandshakeValue);
            return "firmware handshake set (MSI_Software[0] " + current + " -> " + RegisterMap.HandshakeValue + ")";
        }
    }
}
