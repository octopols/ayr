using System.Collections.Generic;
using System.Linq;
using Ayr.Apple;

namespace Ayr.Ui
{
    /// <summary>The tray popup's one-line view of Apple fast charge.</summary>
    public sealed class AppleSummary
    {
        public AppleSummary(bool automatic, IReadOnlyList<AppleDeviceStatus> devices)
        {
            Automatic = automatic;
            Connected = devices.Count;
            FastCharging = devices.Count(status => status.State == FastChargeState.Requested);
        }

        public bool Automatic { get; }
        public int Connected { get; }
        public int FastCharging { get; }

        public string Description
        {
            get
            {
                if (Connected == 0)
                {
                    return "No device connected";
                }
                string devices = Connected == 1 ? "1 device" : Connected + " devices";
                string charging = FastCharging == Connected ? "fast charging"
                    : FastCharging == 0 ? "not fast charging"
                    : FastCharging + " fast charging";
                return devices + " · " + charging;
            }
        }
    }
}
