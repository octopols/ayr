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
@"ayrctl apple                         fast charge on/off, MSI driver state, connected Apple devices
ayrctl apple list [--all]            connected Apple devices with details (--all: also ones seen before)
ayrctl apple on                      fast charge automatically: one request each time an Apple device is plugged in
ayrctl apple off                     stop sending requests (MSI's driver stays loaded until the next reboot)
ayrctl apple boost                   send one request now (at most one per 5 minutes, like Super Charger)";

        private const string AllFlag = "--all";

        private readonly TextWriter _output;
        private readonly AppleChargeController _charger = new AppleChargeController();

        public AppleCommands(TextWriter output)
        {
            _output = output;
        }

        public static bool NeedsElevation(string subcommand) => subcommand == "boost" || subcommand == "on" || subcommand == "off";

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
                case "boost":
                    return SendNow();
                case "on":
                    _charger.SetAutomatic(true);
                    Supervisor.EnsureBackgroundInstance();
                    _output.WriteLine("Fast charge is on: each Apple device gets one request about {0} s after it is plugged in.",
                        AppleChargeController.SettleTime.TotalSeconds);
                    return ExitCode.Success;
                case "off":
                    _charger.SetAutomatic(false);
                    _output.WriteLine("Fast charge is off: no more requests. MSI's driver is left loaded until the next reboot.");
                    return ExitCode.Success;
                default:
                    _output.WriteLine(Usage);
                    return ExitCode.Usage;
            }
        }

        private ExitCode SendNow()
        {
            ChargeRequestResult result = _charger.SendForConnected();
            _output.WriteLine(result.Message);
            return result.Sent ? ExitCode.Success : ExitCode.Failure;
        }

        private void WriteStatus()
        {
            AppleChargeDriver driver = _charger.Driver;
            string driverState = driver.IsLoaded ? "ipadtst2 loaded (stays until restart)" : "ipadtst2 not loaded yet; loads with the first request";
            List<AppleDevice> devices = _charger.Inventory.List(includeDisconnected: false);
            _output.WriteLine("Fast charge  : {0}", DesiredState.Load().AppleFastCharge ? "on (one request per plug-in)" : "off");
            _output.WriteLine("MSI driver   : {0}", driverState);
            _output.WriteLine("Supervisor   : {0}", Supervisor.IsRunning ? "running" : "not running");
            _output.WriteLine("Connected    : {0}", devices.Count == 0 ? "no Apple device" : devices.Count + " Apple device(s)");
            foreach (AppleDevice device in devices)
            {
                _output.WriteLine("  {0} {1}, last request {2}", device.Model, device.Serial, Format(device.LastRequest));
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
