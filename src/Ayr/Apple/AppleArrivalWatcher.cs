using System;
using System.Collections.Generic;
using System.Linq;

namespace Ayr.Apple
{
    /// <summary>
    /// Replaces Super Charger's detection loop while automatic fast charge is on: every connected Apple device gets its
    /// request once it has finished enumerating, including devices already connected when it was switched on. Unplugging
    /// and replugging counts as a new connection.
    /// </summary>
    public sealed class AppleArrivalWatcher
    {
        // Listing devices queries each one's properties, so a device still waiting is looked at again only this often.
        private static readonly TimeSpan LookAgainAfter = TimeSpan.FromSeconds(5);

        private readonly AppleChargeController _charger;
        private readonly HashSet<string> _needNothingMore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _lastConnected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DateTime _nextLook = DateTime.MinValue;

        public AppleArrivalWatcher(AppleChargeController charger)
        {
            _charger = charger;
        }

        /// <summary>Returns a message when a request was sent, otherwise null.</summary>
        public string Poll()
        {
            HashSet<string> connected = _charger.Inventory.ConnectedInstanceIds();
            _needNothingMore.IntersectWith(connected);
            if (connected.IsSubsetOf(_needNothingMore))
            {
                return null;
            }
            if (connected.SetEquals(_lastConnected) && DateTime.Now < _nextLook)
            {
                return null;
            }
            _lastConnected = connected;

            List<AppleDeviceStatus> statuses = _charger.ConnectedStatuses();
            _needNothingMore.UnionWith(statuses
                .Where(status => status.State == FastChargeState.Requested || status.State == FastChargeState.UnsupportedPort)
                .Select(status => status.Device.InstanceId));
            _nextLook = NextLook(statuses);
            if (!statuses.Any(status => status.State == FastChargeState.Ready))
            {
                return null;
            }
            ChargeRequestResult result = _charger.Send(statuses);
            return result.Sent ? result.Message : null;
        }

        // Devices only waiting out the minimum gap need no attention until it has passed.
        private DateTime NextLook(List<AppleDeviceStatus> statuses)
        {
            DateTime soon = DateTime.Now + LookAgainAfter;
            bool onlyWaitingForGap = statuses.Any(status => status.State == FastChargeState.WaitingForGap)
                && !statuses.Any(status => status.State == FastChargeState.Settling || status.State == FastChargeState.Ready);
            return onlyWaitingForGap && _charger.NextRequestAllowedAt > soon ? _charger.NextRequestAllowedAt : soon;
        }
    }
}
