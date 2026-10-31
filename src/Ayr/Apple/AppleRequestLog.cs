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

        public void Record(DateTime when, IEnumerable<string> instanceIds)
        {
            Dictionary<string, string> entries = _file.Read();
            string stamp = when.ToString(TimeFormat, CultureInfo.InvariantCulture);
            entries[LastRequestKey] = stamp;
            foreach (string instanceId in instanceIds)
            {
                entries[instanceId] = stamp;
            }
            _file.Write(entries);
        }

        private DateTime? Get(string key)
        {
            if (_file.Read().TryGetValue(key, out string raw)
                && DateTime.TryParseExact(raw, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime when))
            {
                return when;
            }
            return null;
        }
    }
}
