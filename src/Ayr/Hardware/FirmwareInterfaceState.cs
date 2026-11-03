namespace Ayr.Hardware
{
    public enum FirmwareInterfaceState
    {
        Ready,
        RestartRequired,
        NotAnMsiLaptop,

        /// <summary>An MSI laptop whose firmware Ayr can't control yet; everything that doesn't need it still works.</summary>
        NewerFirmware
    }
}
