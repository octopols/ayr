using System;
using Ayr.Apple;

namespace Ayr.Ui
{
    /// <summary>
    /// One Apple device row. With automatic fast charge on, the row only reports progress; with it off, it has a Send
    /// button, which stays greyed out once the request has been sent for the current connection. Every wait is spelled
    /// out on the row itself with a countdown, not only in a tooltip, so a greyed-out button never looks broken.
    /// </summary>
    public static class AppleRowContent
    {
        public const string SendLabel = "Send";
        public const string SentLabel = "Sent";
        private const string FastChargingBadge = "Fast charging";

        public static readonly RowContent NoDevice = new RowContent("No device connected", "Plug in an iPhone or iPad");

        public static RowContent For(AppleDeviceStatus status, bool automatic, DateTime nextRequestAllowedAt, DateTime now)
        {
            string name = status.Device.Model;
            switch (status.State)
            {
                case FastChargeState.UnsupportedPort:
                    return new RowContent(name, "This USB port can't fast charge. Try another port.");
                case FastChargeState.Settling:
                    string readyIn = Seconds(SettleRemaining(status, now));
                    return automatic
                        ? new RowContent(name, "Just plugged in. Sending in " + readyIn)
                        : Blocked(name, "Just plugged in. Ready to send in " + readyIn, readyIn,
                            "A device needs about " + AppleChargeController.SettleTime.TotalSeconds
                            + " seconds after plugging in before it accepts the request. The button turns on by itself.");
                case FastChargeState.WaitingForGap:
                    string nextAt = "at " + nextRequestAllowedAt.ToString("HH:mm") + " (" + Minutes(nextRequestAllowedAt - now) + ")";
                    return automatic
                        ? new RowContent(name, "Only one request per " + GapMinutes + " min. Sending " + nextAt)
                        : Blocked(name, "Only one request per " + GapMinutes + " min. Send again " + nextAt, SendLabel,
                            "MSI's charger accepts one request every " + GapMinutes + " minutes, so Ayr waits between them.");
                case FastChargeState.Ready:
                    return automatic
                        ? new RowContent(name, "Sending now")
                        : new RowContent(name, "Not fast charging yet", null, BadgeIcon.Bolt, RowButton.Available, SendLabel, null);
                case FastChargeState.Requested:
                    string sentAt = "Request sent at " + status.Device.LastRequest.GetValueOrDefault().ToString("HH:mm");
                    return automatic
                        ? new RowContent(name, sentAt, FastChargingBadge, BadgeIcon.Bolt, RowButton.Hidden, null, null)
                        : new RowContent(name, sentAt + ". Replug to send again", FastChargingBadge, BadgeIcon.Bolt, RowButton.Blocked, SentLabel,
                            "Already sent while this device is connected. Unplug it and plug it back in to send again.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status.State, "Unknown fast-charge state.");
            }
        }

        /// <summary>True while the row shows a countdown that moves on its own and needs redrawing every second.</summary>
        public static bool IsCountingDown(AppleDeviceStatus status) =>
            status.State == FastChargeState.Settling || status.State == FastChargeState.WaitingForGap;

        /// <summary>True once a countdown shown from this status has run out, so the state must be read again.</summary>
        public static bool CountdownEnded(AppleDeviceStatus status, DateTime nextRequestAllowedAt, DateTime now)
        {
            switch (status.State)
            {
                case FastChargeState.Settling:
                    return SettleRemaining(status, now) <= TimeSpan.Zero;
                case FastChargeState.WaitingForGap:
                    return now >= nextRequestAllowedAt;
                default:
                    return false;
            }
        }

        private static double GapMinutes => AppleChargeDriver.MinimumGap.TotalMinutes;

        private static TimeSpan SettleRemaining(AppleDeviceStatus status, DateTime now) =>
            status.Device.LastArrival.GetValueOrDefault(now) + AppleChargeController.SettleTime - now;

        private static string Seconds(TimeSpan remaining) => Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)) + " s";

        private static string Minutes(TimeSpan remaining) =>
            remaining.TotalMinutes < 1 ? "in under a minute" : "in " + (int)Math.Ceiling(remaining.TotalMinutes) + " min";

        private static RowContent Blocked(string name, string detail, string buttonText, string reason) =>
            new RowContent(name, detail, null, BadgeIcon.Bolt, RowButton.Blocked, buttonText, reason);
    }
}
