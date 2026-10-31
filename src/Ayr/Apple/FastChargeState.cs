namespace Ayr.Apple
{
    /// <summary>Where a connected Apple device stands with its fast-charge request.</summary>
    public enum FastChargeState
    {
        /// <summary>Its USB hub driver isn't one MSI's driver works with, so it can never get the request.</summary>
        UnsupportedPort,

        /// <summary>Plugged in moments ago; nothing is sent until USB enumeration has settled.</summary>
        Settling,

        /// <summary>Not fast charging, but the minimum gap since the last request hasn't passed yet.</summary>
        WaitingForGap,

        /// <summary>Not fast charging, and a request can be sent now.</summary>
        Ready,

        /// <summary>The request was sent since it was plugged in; it lasts until the device is unplugged.</summary>
        Requested
    }
}
