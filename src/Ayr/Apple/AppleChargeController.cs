using System;
using System.Collections.Generic;
using System.Linq;
using Ayr.App;

namespace Ayr.Apple
{
    /// <summary>
    /// Apple fast charge, replacing MSI Super Charger's service: one request when an Apple device is plugged in (the
    /// supervisor watches for that) or on demand. MSI's driver is never stopped; see AppleChargeDriver for why.
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

        /// <summary>
        /// Sends one request when an Apple device on a supported hub is connected and settled. MSI's driver acts on the
        /// first Apple device it finds, so with several connected only one is reached per request.
        /// </summary>
        public ChargeRequestResult SendForConnected()
        {
            List<AppleDevice> devices = Inventory.List(includeDisconnected: false).Where(device => AppleChargeDriver.SupportsHub(device.HubService)).ToList();
            if (devices.Count == 0)
            {
                return ChargeRequestResult.NotSent("No Apple device on a supported USB hub is connected; nothing sent.");
            }
            // MSI's driver walks the hub's device list without locking it, so nothing is sent while a device enumerates.
            AppleDevice settling = devices.FirstOrDefault(device => device.IsSettling(SettleTime));
            if (settling != null)
            {
                return ChargeRequestResult.NotSent("Not sent: " + settling.Model + " connected less than " + SettleTime.TotalSeconds + " s ago.");
            }
            if (!Driver.TrySend(_requests, out string refusal))
            {
                return ChargeRequestResult.NotSent("Not sent: " + refusal + ".");
            }
            DateTime now = DateTime.Now;
            foreach (AppleDevice device in devices)
            {
                _requests.RecordRequestFor(device.InstanceId, now);
            }
            string names = string.Join(", ", devices.Select(device => device.Model + " " + device.Serial));
            ActivityLog.Write("apple charge request sent for " + names);
            string severalNote = devices.Count > 1 ? " (several Apple devices connected: MSI's driver reaches only the first one)" : "";
            return ChargeRequestResult.Success("Fast-charge request sent: " + names + severalNote + ".");
        }

        public void SetAutomatic(bool on)
        {
            DesiredState.Update(state => state.AppleFastCharge = on);
            ActivityLog.Write("apple fast charge " + (on ? "on" : "off"));
        }
    }
}
