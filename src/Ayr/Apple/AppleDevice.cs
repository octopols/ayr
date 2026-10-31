using System;
using System.Collections.Generic;

namespace Ayr.Apple
{
    /// <summary>One Apple USB device: the composite parent node, not its MTP/PTP interfaces.</summary>
    public sealed class AppleDevice
    {
        // Super Charger.ini [FASTER_CHARGING_LIST]; 12A8 is shared by every iPhone since the iPhone 5.
        private static readonly Dictionary<string, string> KnownModels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["129A"] = "iPad",
            ["1297"] = "iPhone 4",
            ["129F"] = "iPad 2",
            ["12A0"] = "iPhone 4S",
            ["12A4"] = "iPad (3rd gen) Wi-Fi",
            ["12A6"] = "iPad (3rd gen) Wi-Fi + 4G",
            ["12AB"] = "iPad (4th gen) / iPad mini",
            ["12A8"] = "iPhone (iPhone 5 or newer)"
        };

        public AppleDevice(string instanceId, string productId, string serial, bool present)
        {
            InstanceId = instanceId;
            ProductId = productId;
            Serial = serial;
            Present = present;
        }

        public string InstanceId { get; }
        public string ProductId { get; }
        public string Serial { get; }
        public bool Present { get; }
        public string Revision { get; set; }
        public string ReportedName { get; set; }
        public string Driver { get; set; }
        public string DriverVersion { get; set; }
        public string LowerFilters { get; set; }
        public string HubService { get; set; }
        public string Port { get; set; }
        public string Location { get; set; }
        public List<string> Interfaces { get; } = new List<string>();
        public DateTime? FirstInstalled { get; set; }
        public DateTime? LastArrival { get; set; }
        public DateTime? LastRequest { get; set; }

        public string Model => KnownModels.TryGetValue(ProductId, out string name) ? name : "Apple device (PID " + ProductId + ")";

        public bool IsSettling(TimeSpan settleTime) => LastArrival.HasValue && DateTime.Now - LastArrival.Value < settleTime;
    }
}
