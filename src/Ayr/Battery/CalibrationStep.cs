namespace Ayr.Battery
{
    /// <summary>Where a calibration is. The steps follow MSI's: charge to full, empty to 5%, then charge again.</summary>
    public enum CalibrationStep
    {
        Idle,

        /// <summary>The firmware won't top up a battery that is already 95 to 99%, so it is first run down to 90%.</summary>
        EasingOffFull,

        ChargingToFull,
        Emptying,
        Recharging
    }
}
