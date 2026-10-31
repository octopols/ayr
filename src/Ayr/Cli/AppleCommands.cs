using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ayr.Agent;
using Ayr.App;
using Ayr.Apple;

namespace Ayr.Cli
{
    /// <summary>"ayrctl apple ...": Apple fast charge through MSI Super Charger's own helper, and device details.</summary>
    public sealed class AppleCommands
    {
        public const string Usage =
@"ayrctl apple                         automatic fast charge on/off, MSI driver state, connected Apple devices
ayrctl apple list [--all]            connected Apple devices with details (--all: also ones seen before)
ayrctl apple on                      send the fast-charge request automatically to every Apple device you connect
ayrctl apple off                     stop sending automatically (devices already fast charging stay so until unplugged)
ayrctl apple send                    send the request now (once per connection, at most every 5 minutes)";

        private const string AllFlag = "--all";

        private readonly TextWriter _output;
        private readonly AppleChargeController _charger = new AppleChargeController();

        public AppleCommands(TextWriter output)
        {
            _output = output;
        }

        public static bool NeedsElevation(string subcommand) => subcommand == "send" || subcommand == "on" || subcommand == "off";

        public ExitCode Execute(string subcommand, IReadOnlyList<string> args)
        {
            switch (subcommand)
            {
                case null:
                case "status":
                    WriteStatus();
                    return ExitCode.Success;
                case "list":
                    WriteDevices(includeDisconnected: args.Contains(AllFlag));
                    return ExitCode.Success;
                case "send":
                    return SendNow();
                case "on":
                    return TurnOn();
                case "off":
                    _charger.SetAutomatic(false);
                    _output.WriteLine("Automatic fast charge is off. Devices already fast charging stay so until unplugged.");
                    return ExitCode.Success;
                default:
                    _output.WriteLine(Usage);
                    return ExitCode.Usage;
            }
        }

        private ExitCode TurnOn()
        {
            _charger.SetAutomatic(true);
            Supervisor.EnsureBackgroundInstance();
            _output.WriteLine("Automatic fast charge is on: each Apple device gets the request about {0} s after it is plugged in.",
                AppleChargeController.SettleTime.TotalSeconds);
            ChargeRequestResult sentNow = _charger.SendIfAnyReady();
            if (sentNow != null)
            {
                _output.WriteLine(sentNow.Message);
            }
            return ExitCode.Success;
        }

        private ExitCode SendNow()
        {
            ChargeRequestResult result = _charger.SendForConnected();
            _output.WriteLine(result.Sent ? result.Message : "Not sent: " + result.Message);
            return result.Sent ? ExitCode.Success : ExitCode.Failure;
        }

        private void WriteStatus()
        {
            AppleChargeDriver driver = _charger.Driver;
            string driverState = driver.IsLoaded ? "ipadtst2 loaded (stays until restart)" : "ipadtst2 not loaded yet; loads with the first request";
            List<AppleDeviceStatus> devices = _charger.ConnectedStatuses();
            _output.WriteLine("Automatic    : {0}", DesiredState.Load().AppleAutoFastCharge ? "on (one request per device per connection)" : "off");
            _output.WriteLine("MSI driver   : {0}", driverState);
            _output.WriteLine("Supervisor   : {0}", Supervisor.IsRunning ? "running" : "not running");
            _output.WriteLine("Connected    : {0}", devices.Count == 0 ? "no Apple device" : devices.Count + " Apple device(s)");
            foreach (AppleDeviceStatus status in devices)
            {
                _output.WriteLine("  {0} {1}: {2}", status.Device.Model, status.Device.Serial, Describe(status));
            }
        }

        private string Describe(AppleDeviceStatus status)
        {
            switch (status.State)
            {
                case FastChargeState.UnsupportedPort:
                    return "this USB port can't fast charge";
                case FastChargeState.Settling:
                    return "just connected";
                case FastChargeState.WaitingForGap:
                    return "not fast charging; next request possible at " + Format(_charger.NextRequestAllowedAt);
                case FastChargeState.Ready:
                    return "not fast charging";
                case FastChargeState.Requested:
                    return "fast charging (request sent " + Format(status.Device.LastRequest) + ")";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status.State, "Unknown fast-charge state.");
            }
        }

        private void WriteDevices(bool includeDisconnected)
        {
            List<AppleDevice> devices = _charger.Inventory.List(includeDisconnected);
            if (devices.Count == 0)
            {
                _output.WriteLine(includeDisconnected
                    ? "No Apple device has been connected to this PC."
                    : "No Apple device connected. Use --all for devices seen before.");
                return;
            }
            foreach (AppleDevice device in devices)
            {
                WriteDevice(device);
                _output.WriteLine();
            }
        }

        private void WriteDevice(AppleDevice device)
        {
            _output.WriteLine("{0}  [{1}]", device.Model, device.Present ? "connected" : "not connected");
            WriteRow("Reports as", device.ReportedName);
            WriteRow("USB ID", "05AC:" + device.ProductId + (device.Revision != null ? "  rev " + device.Revision : ""));
            WriteRow("Serial", device.Serial);
            WriteRow("Driver", device.Driver
                + (device.DriverVersion != null ? " " + device.DriverVersion : "")
                + (device.LowerFilters != null ? ", filter " + device.LowerFilters : ""));
            if (device.Present)
            {
                string hubSupport = AppleChargeDriver.SupportsHub(device.HubService) ? " (fast charge supported)" : " (fast charge NOT supported on this hub)";
                WriteRow("Interfaces", device.Interfaces.Count == 0 ? null : string.Join("; ", device.Interfaces));
                WriteRow("Port", (device.Port ?? "?") + "  (" + device.Location + ")");
                WriteRow("Hub driver", device.HubService + hubSupport);
                WriteRow("First seen", Format(device.FirstInstalled));
                WriteRow("Connected at", Format(device.LastArrival));
            }
            WriteRow("Last request", Format(device.LastRequest));
            WriteRow("Instance", device.InstanceId);
        }

        private void WriteRow(string label, string value) =>
            _output.WriteLine("  {0,-14}: {1}", label, string.IsNullOrEmpty(value) ? "-" : value);

        private static string Format(DateTime? when) =>
            when?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "never";
    }
}
