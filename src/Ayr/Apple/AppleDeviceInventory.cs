using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Ayr.Apple
{
    /// <summary>
    /// Read-only view of Apple USB devices: connected ones from WMI (Win32_PnPEntity and its device properties), ones seen
    /// before from the registry's USB enumeration.
    /// </summary>
    public sealed class AppleDeviceInventory
    {
        private const string EnumUsbKey = @"SYSTEM\CurrentControlSet\Enum\USB";
        private const string AppleVendor = "VID_05AC";
        private const string InterfaceMarker = "&MI_";
        private const string ParentKey = "DEVPKEY_Device_Parent";
        private const string ReportedNameKey = "DEVPKEY_Device_BusReportedDeviceDesc";
        private const string PortKey = "DEVPKEY_Device_BiosDeviceName";
        private const string LocationKey = "DEVPKEY_Device_LocationInfo";
        private const string LowerFiltersKey = "DEVPKEY_Device_LowerFilters";
        private const string DriverVersionKey = "DEVPKEY_Device_DriverVersion";
        private const string FirstInstallKey = "DEVPKEY_Device_FirstInstallDate";
        private const string LastArrivalKey = "DEVPKEY_Device_LastArrivalDate";
        private const string HardwareIdsKey = "DEVPKEY_Device_HardwareIds";

        private static readonly string[] DescribedKeys =
        {
            ParentKey, ReportedNameKey, PortKey, LocationKey, LowerFiltersKey, DriverVersionKey, FirstInstallKey, LastArrivalKey, HardwareIdsKey
        };

        private static readonly Regex DeviceIdPattern = new Regex(@"^USB\\VID_05AC&PID_([0-9A-F]{4})\\(.+)$", RegexOptions.IgnoreCase);
        private static readonly Regex RevisionPattern = new Regex(@"REV_([0-9A-F]{4})", RegexOptions.IgnoreCase);

        private readonly AppleRequestLog _requests;

        public AppleDeviceInventory(AppleRequestLog requests)
        {
            _requests = requests;
        }

        /// <summary>Instance IDs of connected Apple devices; cheap enough for the supervisor to poll.</summary>
        public HashSet<string> ConnectedInstanceIds() =>
            new HashSet<string>(QueryAppleEntities().Select(DeviceId).Where(id => DeviceIdPattern.IsMatch(id)), StringComparer.OrdinalIgnoreCase);

        public List<AppleDevice> List(bool includeDisconnected)
        {
            List<ManagementObject> entities = QueryAppleEntities().ToList();
            List<AppleDevice> devices = entities
                .Where(entity => DeviceIdPattern.IsMatch(DeviceId(entity)))
                .Select(entity => DescribeConnected(entity, entities))
                .ToList();
            if (includeDisconnected)
            {
                var connected = new HashSet<string>(devices.Select(device => device.InstanceId), StringComparer.OrdinalIgnoreCase);
                devices.AddRange(PreviouslySeen().Where(device => !connected.Contains(device.InstanceId)));
            }
            foreach (AppleDevice device in devices)
            {
                device.LastRequest = _requests.LastRequestFor(device.InstanceId);
            }
            return devices;
        }

        private static AppleDevice DescribeConnected(ManagementObject entity, List<ManagementObject> allAppleEntities)
        {
            string instanceId = DeviceId(entity);
            Match id = DeviceIdPattern.Match(instanceId);
            Dictionary<string, object> properties = DeviceProperties(entity, DescribedKeys);
            Match revision = RevisionPattern.Match(Text(properties, HardwareIdsKey) ?? "");
            var device = new AppleDevice(instanceId, id.Groups[1].Value.ToUpperInvariant(), id.Groups[2].Value, present: true)
            {
                Driver = entity["Service"] as string,
                ReportedName = Text(properties, ReportedNameKey),
                Port = Text(properties, PortKey),
                Location = Text(properties, LocationKey),
                LowerFilters = Text(properties, LowerFiltersKey),
                DriverVersion = Text(properties, DriverVersionKey),
                FirstInstalled = Date(properties, FirstInstallKey),
                LastArrival = Date(properties, LastArrivalKey),
                Revision = revision.Success ? revision.Groups[1].Value : null,
                HubService = ServiceOf(Text(properties, ParentKey))
            };
            string interfacePrefix = instanceId.Substring(0, instanceId.IndexOf('\\', "USB\\".Length)) + InterfaceMarker;
            IEnumerable<ManagementObject> interfaces = allAppleEntities.Where(candidate =>
                DeviceId(candidate).StartsWith(interfacePrefix, StringComparison.OrdinalIgnoreCase) && IsChildOf(candidate, instanceId));
            device.Interfaces.AddRange(interfaces.Select(child => child["Name"] + " (" + child["Service"] + ")"));
            return device;
        }

        private static bool IsChildOf(ManagementObject candidate, string parentInstanceId) =>
            string.Equals(Text(DeviceProperties(candidate, ParentKey), ParentKey), parentInstanceId, StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<AppleDevice> PreviouslySeen()
        {
            using (RegistryKey usb = Registry.LocalMachine.OpenSubKey(EnumUsbKey))
            {
                if (usb == null)
                {
                    yield break;
                }
                IEnumerable<string> appleHardwareKeys = usb.GetSubKeyNames().Where(name =>
                    name.StartsWith(AppleVendor, StringComparison.OrdinalIgnoreCase)
                    && name.IndexOf(InterfaceMarker, StringComparison.OrdinalIgnoreCase) < 0);
                foreach (string hardware in appleHardwareKeys)
                {
                    using (RegistryKey hardwareKey = usb.OpenSubKey(hardware))
                    {
                        foreach (string serial in hardwareKey.GetSubKeyNames())
                        {
                            string instanceId = @"USB\" + hardware + @"\" + serial;
                            Match id = DeviceIdPattern.Match(instanceId);
                            if (!id.Success)
                            {
                                continue;
                            }
                            using (RegistryKey instance = hardwareKey.OpenSubKey(serial))
                            {
                                yield return new AppleDevice(instanceId, id.Groups[1].Value.ToUpperInvariant(), serial, present: false)
                                {
                                    Driver = instance.GetValue("Service") as string,
                                    ReportedName = ReadableDescription(instance.GetValue("DeviceDesc") as string)
                                };
                            }
                        }
                    }
                }
            }
        }

        // Registry descriptions look like "@oem44.inf,%resource%;Fallback text"; the fallback is the readable part.
        private static string ReadableDescription(string raw)
        {
            if (raw == null || !raw.StartsWith("@", StringComparison.Ordinal))
            {
                return raw;
            }
            return raw.Substring(raw.LastIndexOf(';') + 1);
        }

        private static IEnumerable<ManagementObject> QueryAppleEntities()
        {
            // A leading-wildcard LIKE avoids WQL's backslash escaping in device IDs.
            using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT DeviceID, Name, Service FROM Win32_PnPEntity WHERE DeviceID LIKE '%" + AppleVendor + "%'"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                return results.Cast<ManagementObject>().ToList();
            }
        }

        private static string ServiceOf(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return null;
            }
            string escaped = instanceId.Replace("\\", "\\\\").Replace("'", "\\'");
            using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Service FROM Win32_PnPEntity WHERE DeviceID = '" + escaped + "'"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                return results.Cast<ManagementObject>().Select(hub => hub["Service"] as string).FirstOrDefault();
            }
        }

        private static string DeviceId(ManagementObject entity) => (string)entity["DeviceID"];

        private static Dictionary<string, object> DeviceProperties(ManagementObject entity, params string[] keys)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            ManagementBaseObject arguments = entity.GetMethodParameters("GetDeviceProperties");
            arguments["devicePropertyKeys"] = keys;
            ManagementBaseObject output = entity.InvokeMethod("GetDeviceProperties", arguments, null);
            if (!(output["deviceProperties"] is ManagementBaseObject[] properties))
            {
                return result;
            }
            foreach (ManagementBaseObject property in properties)
            {
                bool hasData = property.Properties.Cast<PropertyData>().Any(data => data.Name == "Data");
                if (property["KeyName"] is string key && hasData)
                {
                    result[key] = property["Data"];
                }
            }
            return result;
        }

        private static string Text(Dictionary<string, object> properties, string key)
        {
            if (!properties.TryGetValue(key, out object value) || value == null)
            {
                return null;
            }
            return value is string[] list ? string.Join(" ", list) : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static DateTime? Date(Dictionary<string, object> properties, string key)
        {
            string raw = Text(properties, key);
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }
            try
            {
                // WMI returns FILETIME device properties as DMTF datetime strings.
                return ManagementDateTimeConverter.ToDateTime(raw);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is FormatException)
            {
                return null;
            }
        }
    }
}
