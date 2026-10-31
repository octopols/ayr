using System;
using System.Collections.Generic;
using System.Globalization;
using Ayr.App;

namespace Ayr.Apple
{
    /// <summary>
    /// When fast-charge requests were sent: per device, and the last request overall, which enforces the minimum gap
    /// between requests across processes and reboots.
    /// </summary>
    public sealed class AppleRequestLog
    {
        private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss";
        private const string LastRequestKey = "_last_send";

        private readonly KeyValueFile _file = new KeyValueFile(AppPaths.AppleRequestsFile);

        public DateTime? LastRequest => Get(LastRequestKey);

        public DateTime? LastRequestFor(string instanceId) => Get(instanceId);

        public void RecordRequest(DateTime when) => Set(LastRequestKey, when);

        public void RecordRequestFor(string instanceId, DateTime when) => Set(instanceId, when);

        private DateTime? Get(string key)
        {
            if (_file.Read().TryGetValue(key, out string raw)
                && DateTime.TryParseExact(raw, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime when))
            {
                return when;
            }
            return null;
        }

        private void Set(string key, DateTime when)
        {
            Dictionary<string, string> entries = _file.Read();
            entries[key] = when.ToString(TimeFormat, CultureInfo.InvariantCulture);
            _file.Write(entries);
        }
    }
}
