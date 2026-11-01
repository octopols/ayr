using System;
using Ayr.Apple;

namespace Ayr.Ui
{
    /// <summary>
    /// One Apple device row. With automatic fast charge on, the row only reports progress; with it off, it has a Send
    /// button, which stays greyed out once the request has been sent for the current connection.
    /// </summary>
    public static class AppleRowContent
    {
        public const string SendLabel = "Send";
        public const string SentLabel = "Sent";
        private const string FastChargingBadge = "Fast charging";

        public static readonly RowContent NoDevice = new RowContent("No device connected", "Plug in an iPhone or iPad");

        public static RowContent For(AppleDeviceStatus status, bool automatic, DateTime nextRequestAllowedAt)
        {
            string name = status.Device.Model;
            string nextRequest = nextRequestAllowedAt.ToString("HH:mm");
            switch (status.State)
            {
                case FastChargeState.UnsupportedPort:
                    return new RowContent(name, "This USB port can't fast charge");
                case FastChargeState.Settling:
                    return automatic
                        ? new RowContent(name, "Sending in a few seconds")
                        : Blocked(name, "Just connected", SendLabel, "Wait a few seconds after plugging it in.");
                case FastChargeState.WaitingForGap:
                    return automatic
                        ? new RowContent(name, "Sending at " + nextRequest)
                        : Blocked(name, "Not fast charging", SendLabel,
                            "Only one request every " + AppleChargeDriver.MinimumGap.TotalMinutes + " minutes. You can send again at " + nextRequest + ".");
                case FastChargeState.Ready:
                    return automatic
                        ? new RowContent(name, "Sending now")
                        : new RowContent(name, "Not fast charging", null, BadgeIcon.Bolt, RowButton.Available, SendLabel, null);
                case FastChargeState.Requested:
                    string sentAt = "Request sent at " + status.Device.LastRequest.GetValueOrDefault().ToString("HH:mm");
                    return automatic
                        ? new RowContent(name, sentAt, FastChargingBadge, BadgeIcon.Bolt, RowButton.Hidden, null, null)
                        : new RowContent(name, sentAt, FastChargingBadge, BadgeIcon.Bolt, RowButton.Blocked, SentLabel,
                            "Already sent while this device is connected. Unplug it and plug it back in to send again.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status.State, "Unknown fast-charge state.");
            }
        }

        private static RowContent Blocked(string name, string detail, string buttonText, string reason) =>
            new RowContent(name, detail, null, BadgeIcon.Bolt, RowButton.Blocked, buttonText, reason);
    }
}
