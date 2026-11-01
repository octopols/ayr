namespace Ayr.Battery
{
    /// <summary>A power plan setting's value while plugged in and while on battery.</summary>
    public sealed class PowerValues
    {
        public PowerValues(uint pluggedIn, uint onBattery)
        {
            PluggedIn = pluggedIn;
            OnBattery = onBattery;
        }

        public uint PluggedIn { get; }
        public uint OnBattery { get; }
    }
}
