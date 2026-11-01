using System;
using System.Collections.Generic;
using System.Linq;
using Ayr.App;

namespace Ayr.Apple
{
    /// <summary>
    /// Apple fast charge, replacing MSI Super Charger's service. A request lasts until the device is unplugged, so each
    /// connected device needs one per connection: sent automatically when that is switched on (the supervisor watches for
    /// devices), or by hand. MSI's driver is never stopped; see AppleChargeDriver for why.
    /// </summary>
    public sealed class AppleChargeController
    {
        public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(15);

        private readonly AppleRequestLog _requests = new AppleRequestLog();

        public AppleChargeController()
        {
            Inventory = new AppleDeviceInventory(_requests);
        }

        public AppleChargeDriver Driver { get; } = new AppleChargeDriver();

        public AppleDeviceInventory Inventory { get; }

        public DateTime NextRequestAllowedAt => (_requests.LastRequest ?? DateTime.MinValue) + AppleChargeDriver.MinimumGap;

        public List<AppleDeviceStatus> ConnectedStatuses()
        {
            DateTime now = DateTime.Now;
            DateTime nextAllowed = NextRequestAllowedAt;
            return Inventory.List(includeDisconnected: false)
                .Select(device => new AppleDeviceStatus(device, StateOf(device, now, nextAllowed)))
                .ToList();
        }

        /// <summary>Sends one request for the connected devices; the result says why when nothing was sent.</summary>
        public ChargeRequestResult SendForConnected() => Send(ConnectedStatuses());

        /// <summary>Sends only when a connected device is waiting for its request; null when none is.</summary>
        public ChargeRequestResult SendIfAnyReady()
        {
            List<AppleDeviceStatus> connected = ConnectedStatuses();
            return connected.Any(status => status.State == FastChargeState.Ready) ? Send(connected) : null;
        }

        /// <summary>
        /// MSI's driver acts on the first Apple device it finds, so with several connected one request reaches only one of
        /// them; it is recorded for all, as there is no telling which.
        /// </summary>
        public ChargeRequestResult Send(IReadOnlyList<AppleDeviceStatus> connected)
        {
            List<AppleDeviceStatus> reachable = connected.Where(status => status.State != FastChargeState.UnsupportedPort).ToList();
            if (reachable.Count == 0)
            {
                return ChargeRequestResult.NotSent("No Apple device on a supported USB port is connected.");
            }
            // MSI's driver walks the hub's device list without locking it, so nothing is sent while a device enumerates.
            AppleDeviceStatus settling = reachable.FirstOrDefault(status => status.State == FastChargeState.Settling);
            if (settling != null)
            {
                return ChargeRequestResult.NotSent(settling.Device.Model + " was plugged in less than " + SettleTime.TotalSeconds + " s ago.");
            }
            if (reachable.All(status => status.State == FastChargeState.Requested))
            {
                return ChargeRequestResult.NotSent("Already sent while connected. Unplug the device and plug it back in to send again.");
            }
            if (!Driver.TrySend(_requests, reachable.Select(status => status.Device.InstanceId), out string refusal))
            {
                return ChargeRequestResult.NotSent(refusal);
            }
            string names = string.Join(", ", reachable.Select(status => status.Device.Model + " " + status.Device.Serial));
            ActivityLog.Write("apple charge request sent for " + names);
            string severalNote = reachable.Count > 1 ? " (several Apple devices connected: MSI's driver reaches only the first one)" : "";
            return ChargeRequestResult.Success("Fast-charge request sent: " + names + severalNote + ".");
        }

        public void SetAutomatic(bool on)
        {
            DesiredState.Update(state => state.AppleAutoFastCharge = on);
            ActivityLog.Write("automatic apple fast charge " + (on ? "on" : "off"));
        }

        private static FastChargeState StateOf(AppleDevice device, DateTime now, DateTime nextAllowed)
        {
            if (!AppleChargeDriver.SupportsHub(device.HubService))
            {
                return FastChargeState.UnsupportedPort;
            }
            if (device.FastChargeRequested)
            {
                return FastChargeState.Requested;
            }
            if (device.IsSettling(SettleTime))
            {
                return FastChargeState.Settling;
            }
            return now < nextAllowed ? FastChargeState.WaitingForGap : FastChargeState.Ready;
        }
    }
}
