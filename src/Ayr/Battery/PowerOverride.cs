namespace Ayr.Battery
{
    /// <summary>A value to hold a power plan setting at while plugged in, on battery, or both; null leaves that case alone.</summary>
    public sealed class PowerOverride
    {
        public PowerOverride(PowerSetting setting, uint? pluggedIn, uint? onBattery)
        {
            Setting = setting;
            PluggedIn = pluggedIn;
            OnBattery = onBattery;
        }

        public PowerSetting Setting { get; }
        public uint? PluggedIn { get; }
        public uint? OnBattery { get; }

        public PowerValues AppliedTo(PowerValues current) =>
            new PowerValues(PluggedIn ?? current.PluggedIn, OnBattery ?? current.OnBattery);
    }
}
