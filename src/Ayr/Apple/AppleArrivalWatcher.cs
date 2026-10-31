using System;
using System.Collections.Generic;
using System.Linq;
using Ayr.App;

namespace Ayr.Apple
{
    /// <summary>
    /// Replaces Super Charger's detection loop: once an Apple device has been connected long enough to finish enumerating,
    /// one request is sent. A device that stays connected gets no more requests; unplugging and replugging counts as new.
    /// A request refused by the minimum gap is retried on later polls.
    /// </summary>
    public sealed class AppleArrivalWatcher
    {
        private readonly AppleChargeController _charger;
        private readonly Dictionary<string, DateTime> _arrivedAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public AppleArrivalWatcher(AppleChargeController charger)
        {
            _charger = charger;
        }

        /// <summary>Returns a message when a request was sent, otherwise null.</summary>
        public string Poll()
        {
            HashSet<string> connected = _charger.Inventory.ConnectedInstanceIds();
            ForgetUnplugged(connected);
            DateTime now = DateTime.Now;
            foreach (string id in connected.Where(id => !_handled.Contains(id) && !_arrivedAt.ContainsKey(id)))
            {
                _arrivedAt[id] = now;
            }
            List<string> settled = _arrivedAt.Where(arrival => now - arrival.Value >= AppleChargeController.SettleTime).Select(arrival => arrival.Key).ToList();
            if (settled.Count == 0)
            {
                return null;
            }
            ChargeRequestResult result = _charger.SendForConnected();
            if (!result.Sent)
            {
                return null;
            }
            foreach (string id in settled)
            {
                _arrivedAt.Remove(id);
                _handled.Add(id);
            }
            ActivityLog.Write("apple device plugged in: " + string.Join(", ", settled));
            return result.Message;
        }

        private void ForgetUnplugged(HashSet<string> connected)
        {
            _handled.IntersectWith(connected);
            foreach (string unplugged in _arrivedAt.Keys.Where(id => !connected.Contains(id)).ToList())
            {
                _arrivedAt.Remove(unplugged);
            }
        }
    }
}
