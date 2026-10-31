namespace Ayr.Ui
{
    public sealed class AppleSummary
    {
        public AppleSummary(bool fastChargeOn, int connectedDevices)
        {
            FastChargeOn = fastChargeOn;
            ConnectedDevices = connectedDevices;
        }

        public bool FastChargeOn { get; }
        public int ConnectedDevices { get; }

        public string Description
        {
            get
            {
                string devices = ConnectedDevices == 0 ? "No device"
                    : ConnectedDevices == 1 ? "1 device connected"
                    : ConnectedDevices + " devices connected";
                return devices + " · " + (FastChargeOn ? "On" : "Off");
            }
        }
    }
}
