namespace Ayr.Apple
{
    public sealed class AppleDeviceStatus
    {
        public AppleDeviceStatus(AppleDevice device, FastChargeState state)
        {
            Device = device;
            State = state;
        }

        public AppleDevice Device { get; }
        public FastChargeState State { get; }
    }
}
