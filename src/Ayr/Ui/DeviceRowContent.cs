using System;
using Ayr.Apple;

namespace Ayr.Ui
{
    /// <summary>
    /// What one row of the Apple device list shows. With automatic fast charge on, the row only reports progress; with it
    /// off, it has a Send button, which stays greyed out once the request has been sent for the current connection.
    /// </summary>
    public sealed class DeviceRowContent
    {
        public const string SendLabel = "Send";
        public const string SentLabel = "Sent";

        public static readonly DeviceRowContent NoDevice =
            new DeviceRowContent("No device connected", "Plug in an iPhone or iPad", false, SendButton.Hidden, null, null);

        private DeviceRowContent(string name, string detail, bool fastCharging, SendButton send, string sendText, string sendBlockedReason)
        {
            Name = name;
            Detail = detail;
            FastCharging = fastCharging;
            Send = send;
            SendText = sendText;
            SendBlockedReason = sendBlockedReason;
        }

        public string Name { get; }
        public string Detail { get; }
        public bool FastCharging { get; }
        public SendButton Send { get; }
        public string SendText { get; }
        public string SendBlockedReason { get; }

        public static DeviceRowContent For(AppleDeviceStatus status, bool automatic, DateTime nextRequestAllowedAt)
        {
            string name = status.Device.Model;
            string nextRequest = nextRequestAllowedAt.ToString("HH:mm");
            switch (status.State)
            {
                case FastChargeState.UnsupportedPort:
                    return Row(name, "This USB port can't fast charge");
                case FastChargeState.Settling:
                    return automatic
                        ? Row(name, "Sending in a few seconds")
                        : Blocked(name, "Just connected", SendLabel, "Wait a few seconds after plugging it in.");
                case FastChargeState.WaitingForGap:
                    return automatic
                        ? Row(name, "Sending at " + nextRequest)
                        : Blocked(name, "Not fast charging", SendLabel,
                            "Only one request every " + AppleChargeDriver.MinimumGap.TotalMinutes + " minutes. You can send again at " + nextRequest + ".");
                case FastChargeState.Ready:
                    return automatic
                        ? Row(name, "Sending now")
                        : new DeviceRowContent(name, "Not fast charging", false, SendButton.Available, SendLabel, null);
                case FastChargeState.Requested:
                    string sentAt = "Request sent at " + status.Device.LastRequest.GetValueOrDefault().ToString("HH:mm");
                    return automatic
                        ? new DeviceRowContent(name, sentAt, true, SendButton.Hidden, null, null)
                        : new DeviceRowContent(name, sentAt, true, SendButton.Blocked, SentLabel,
                            "Already sent while this device is connected. Unplug it and plug it back in to send again.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status.State, "Unknown fast-charge state.");
            }
        }

        private static DeviceRowContent Row(string name, string detail) =>
            new DeviceRowContent(name, detail, false, SendButton.Hidden, null, null);

        private static DeviceRowContent Blocked(string name, string detail, string sendText, string reason) =>
            new DeviceRowContent(name, detail, false, SendButton.Blocked, sendText, reason);
    }
}
