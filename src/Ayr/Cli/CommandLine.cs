using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using Ayr.Agent;
using Ayr.App;
using Ayr.Controllers;
using Ayr.Diagnostics;
using Ayr.Hardware;
using Ayr.Ui;

namespace Ayr.Cli
{
    public sealed class CommandLine
    {
        private const string Usage =
@"ayrctl status                        fan mode, perf level, temps, duty, RPM, GPU offsets
ayrctl fan auto|max|silent|boost|custom
ayrctl curve                         show the custom fan curves and the temperature of each point
ayrctl curve cpu|gpu <7 values>      set a custom curve in %, e.g. 30,40,50,60,70,85,100, and switch to it
ayrctl perf turbo|high|medium|low     turbo also applies the Turbo GPU offsets
ayrctl perf turbo <core> <memory>     set the Turbo GPU offsets and switch to Turbo
ayrctl oc                            show the Turbo GPU offsets and their limits
ayrctl oc <core> <memory>            set the Turbo GPU offsets in MHz (core 0-180, memory 0-500);
                                     applied at once if Turbo is active, otherwise kept for Turbo
ayrctl apply                         firmware handshake + saved settings (what MSI's services did at boot)
ayrctl charge full|<50-95>           charge the battery to full, or stop at this percentage
ayrctl light off|low|medium|high     keyboard backlight
ayrctl autostart [on|off]            logon task that applies saved settings and runs the background guard
ayrctl tray                          tray icon with the same controls
ayrctl uninstall                     fans to Auto, remove the logon task and Ayr's driver entries
" + GameCommands.Usage + @"
" + AppleCommands.Usage + @"
" + MsiCommands.Usage + @"

Diagnostics
ayrctl probe                         read-only register dump
ayrctl gpu <core> <memory>           set raw GPU offsets, e.g. to simulate a driver reset in the end-to-end test
ayrctl ui-preview [folder]           render the windows to PNG files";

        private readonly TextWriter _output;
        private string[] _args;

        public CommandLine(TextWriter output)
        {
            _output = output;
        }

        public ExitCode Execute(string[] args)
        {
            _args = args;
            string command = Argument(0) ?? "status";
            switch (command)
            {
                case "status":
                    return WithElevationFallback(ShowStatus);
                case "fan":
                    return WithElevation(SetFan);
                case "curve":
                    return CustomCurveCommand();
                case "charge":
                    return ChargeLimitCommand();
                case "light":
                    return KeyboardLightCommand();
                case "perf":
                    return SetPerformanceCommand();
                case "oc":
                    return TurboOffsetsCommand();
                case "apply":
                    return WithElevation(ApplySaved);
                case "autostart":
                    return WithElevation(Autostart);
                case "uninstall":
                    return WithElevation(Uninstall);
                case "game":
                    return GameCommand();
                case "apple":
                    return AppleCommand();
                case "msi":
                    return WithElevation(() => new MsiCommands(_output).Execute(_args.Skip(1).Select(argument => argument.ToLowerInvariant()).ToList()));
                case "supervise":
                    return RunSupervisor();
                case "sentinel":
                    return RunSentinel();
                case "probe":
                    new ReadOnlyProbe(new HardwareContext(), _output).Run();
                    return ExitCode.Success;
                case "gpu":
                    return SetRawGpuOffsetsCommand();
                case "ui-preview":
                    new UiPreview(Argument(1) ?? AppPaths.PreviewDirectory).Run(_output);
                    return ExitCode.Success;
                case "help":
                case "-h":
                case "--help":
                case "/?":
                    _output.WriteLine(Usage);
                    return ExitCode.Success;
                default:
                    _output.WriteLine(Usage);
                    return ExitCode.Usage;
            }
        }

        private string Argument(int index) => index < _args.Length ? _args[index].ToLowerInvariant() : null;

        private ExitCode ShowStatus()
        {
            var hardware = new HardwareContext();
            bool ecReachable = hardware.Fans.TryReadCoolerBoostRegister().HasValue;
            if (!ecReachable && !Elevation.IsElevated)
            {
                return RelaunchElevated();
            }
            new StatusReport(hardware).Write(_output);
            return ExitCode.Success;
        }

        private ExitCode SetFan()
        {
            if (!Enum.TryParse(Argument(1), true, out FanMode mode) || !FanController.SelectableModes.Contains(mode))
            {
                _output.WriteLine("fan mode must be one of: auto, max, silent, boost, custom");
                return ExitCode.Usage;
            }
            var hardware = new HardwareContext();
            var modes = new ModeService(hardware);
            modes.ApplyFan(mode);
            if (mode == FanMode.Silent)
            {
                try
                {
                    Supervisor.EnsureBackgroundInstance();
                }
                catch (Exception ex)
                {
                    // Silent must never stay on without its guard, whatever stopped the guard from starting.
                    modes.FailsafeAuto("could not start Silent guard: " + ex.Message);
                    _output.WriteLine("Could not start the Silent temperature guard; fans set to Auto instead. " + ex.Message);
                    return ExitCode.Failure;
                }
            }
            new StatusReport(hardware).Write(_output);
            return ExitCode.Success;
        }

        // Values are validated before any elevation prompt, so a typo never costs a UAC click.
        private ExitCode CustomCurveCommand()
        {
            string fanName = Argument(1);
            if (fanName == null)
            {
                return WithElevationFallback(ShowCustomCurves);
            }
            if (!Enum.TryParse(fanName, true, out FanKind fan) || !FanCurves.TryParse(Argument(2), out byte[] curve))
            {
                _output.WriteLine("usage: ayrctl curve cpu|gpu <7 values from 0 to 100, separated by commas>");
                return ExitCode.Usage;
            }
            return WithElevation(() =>
            {
                FanCurves saved = DesiredState.Load().CustomCurves;
                FanCurves wanted = fan == FanKind.Cpu ? new FanCurves(curve, saved.Gpu) : new FanCurves(saved.Cpu, curve);
                new ModeService(new HardwareContext()).ApplyCustomCurves(wanted);
                return ShowCustomCurves();
            });
        }

        private ExitCode ShowCustomCurves()
        {
            var hardware = new HardwareContext();
            FanCurves curves = DesiredState.Load().CustomCurves;
            foreach (FanKind fan in new[] { FanKind.Cpu, FanKind.Gpu })
            {
                int[] temperatures = hardware.Fans.ReadPointTemperatures(fan);
                IReadOnlyList<byte> duties = curves.For(fan);
                string points = string.Join("  ", temperatures.Select((temperature, point) =>
                    (point == 0 ? "below " + temperatures[1] : "from " + temperature) + "°C: " + duties[point] + "%"));
                _output.WriteLine("{0}  {1}", fan.ToString().ToUpperInvariant(), points);
            }
            _output.WriteLine("The two hottest points never go below Auto's speed, and no point is lower than the one before it.");
            return ExitCode.Success;
        }

        private ExitCode ChargeLimitCommand()
        {
            int percent = ChargeLimitController.Full;
            bool valid = Argument(1) == "full" || (int.TryParse(Argument(1), out percent) && ChargeLimitController.IsValid(percent));
            if (!valid)
            {
                _output.WriteLine("usage: ayrctl charge full|<{0}-{1}>", ChargeLimitController.MinimumCustom, ChargeLimitController.MaximumCustom);
                return ExitCode.Usage;
            }
            return WithElevation(() =>
            {
                new ModeService(new HardwareContext()).ApplyChargeLimit(percent);
                _output.WriteLine(percent == ChargeLimitController.Full ? "The battery charges to full." : "The battery stops charging at " + percent + "%.");
                return ExitCode.Success;
            });
        }

        private ExitCode KeyboardLightCommand()
        {
            int level = KeyboardLightController.LevelNames.Select(name => name.ToLowerInvariant()).ToList().IndexOf(Argument(1) ?? "");
            if (level < 0)
            {
                _output.WriteLine("usage: ayrctl light off|low|medium|high");
                return ExitCode.Usage;
            }
            return WithElevation(() =>
            {
                new ModeService(new HardwareContext()).ApplyKeyboardLight(level);
                _output.WriteLine("Keyboard light: {0}.", KeyboardLightController.LevelNames[level]);
                return ExitCode.Success;
            });
        }

        private ExitCode SetPerformanceCommand()
        {
            if (!TryParseOffsets(firstIndex: 2, out GpuOffsets offsets))
            {
                return ExitCode.Usage;
            }
            if (!Enum.TryParse(Argument(1), true, out PerformanceLevel level) || !PerformanceController.SelectableLevels.Contains(level))
            {
                _output.WriteLine("performance level must be one of: turbo, high, medium, low");
                return ExitCode.Usage;
            }
            if (offsets != null && level != PerformanceLevel.Turbo)
            {
                _output.WriteLine("GPU offsets only apply to turbo.");
                return ExitCode.Usage;
            }
            return WithElevation(() => SetPerformance(level, offsets));
        }

        private ExitCode SetPerformance(PerformanceLevel level, GpuOffsets offsets)
        {
            var hardware = new HardwareContext();
            var modes = new ModeService(hardware);
            if (offsets != null)
            {
                modes.SetTurboOffsets(offsets);
            }
            modes.ApplyPerformance(level);
            if (level == PerformanceLevel.Turbo)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            new StatusReport(hardware).Write(_output);
            return ExitCode.Success;
        }

        private ExitCode TurboOffsetsCommand()
        {
            if (!TryParseOffsets(firstIndex: 1, out GpuOffsets offsets))
            {
                return ExitCode.Usage;
            }
            if (offsets == null)
            {
                _output.WriteLine("Turbo offsets: {0} (limits: core 0-{1}, memory 0-{2} MHz)",
                    DesiredState.Load().TurboOffsets, GpuOffsets.MaxCoreMhz, GpuOffsets.MaxMemoryMhz);
                _output.WriteLine("Change with: ayrctl oc <core> <memory>");
                return ExitCode.Success;
            }
            return WithElevation(() => SetTurboOffsets(offsets));
        }

        private ExitCode SetTurboOffsets(GpuOffsets offsets)
        {
            var hardware = new HardwareContext();
            bool applied = new ModeService(hardware).SetTurboOffsets(offsets);
            _output.WriteLine(applied
                ? "Turbo is active: applied " + offsets + "."
                : "Saved " + offsets + "; applied the next time Turbo is selected.");
            if (applied)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            new StatusReport(hardware).Write(_output);
            return ExitCode.Success;
        }

        /// <summary>Reads "&lt;core&gt; &lt;memory&gt;" from firstIndex on. No arguments there means no offsets were given.</summary>
        private bool TryParseOffsets(int firstIndex, out GpuOffsets offsets)
        {
            offsets = null;
            int count = _args.Length - firstIndex;
            if (count <= 0)
            {
                return true;
            }
            if (count != 2)
            {
                _output.WriteLine("Give both offsets in MHz: <core> <memory>, e.g. 150 400.");
                return false;
            }
            if (!GpuOffsets.TryParse(_args[firstIndex], _args[firstIndex + 1], out offsets, out string error))
            {
                _output.WriteLine(error);
                return false;
            }
            return true;
        }

        private ExitCode ApplySaved()
        {
            var hardware = new HardwareContext();
            foreach (string line in new StartupApplier(hardware).Apply())
            {
                _output.WriteLine(line);
            }
            if (DesiredState.Load().NeedsSupervision)
            {
                Supervisor.EnsureBackgroundInstance();
            }
            new StatusReport(hardware).Write(_output);
            return ExitCode.Success;
        }

        private ExitCode Autostart()
        {
            var task = new AutostartTask();
            switch (Argument(1))
            {
                case "on":
                    task.Install();
                    _output.WriteLine("Autostart on: task '{0}' runs 'Ayr.exe startup' 10 s after you log in.", AutostartTask.TaskName);
                    return ExitCode.Success;
                case "off":
                    task.Remove();
                    _output.WriteLine("Autostart off: task '{0}' removed.", AutostartTask.TaskName);
                    return ExitCode.Success;
                case null:
                    _output.WriteLine("Autostart is {0}.", task.IsInstalled ? "on" : "off");
                    return ExitCode.Success;
                default:
                    _output.WriteLine("autostart takes on or off");
                    return ExitCode.Usage;
            }
        }

        private ExitCode Uninstall()
        {
            try
            {
                new ModeService(new HardwareContext()).FailsafeAuto("uninstall");
                _output.WriteLine("Fans are on Auto.");
            }
            catch (Exception ex)
            {
                _output.WriteLine("Fans could not be set to Auto ({0}); run \"ayrctl fan auto\" before deleting Ayr.", ex.Message);
            }
            new AutostartTask().Remove();
            _output.WriteLine("Removed the logon task.");
            foreach (KernelDriver driver in new[] { KernelDriver.EcAccess, KernelDriver.AppleCharge })
            {
                if (driver.RemoveIfOwned())
                {
                    _output.WriteLine("Removed the {0} driver entry; the driver unloads at the next restart.", driver.ServiceName);
                }
            }
            if (FirmwareInterface.RemoveIfOwned())
            {
                _output.WriteLine("Removed Ayr's registration of MSI's firmware interface.");
            }
            _output.WriteLine("Exit the tray, then delete this folder: {0}", AppPaths.ExecutableDirectory);
            return ExitCode.Success;
        }

        private ExitCode GameCommand()
        {
            string subcommand = Argument(1);
            var games = new GameCommands(_output);
            return GameCommands.NeedsElevation(subcommand)
                ? WithElevation(() => games.Execute(subcommand, _args))
                : games.Execute(subcommand, _args);
        }

        private ExitCode AppleCommand()
        {
            string subcommand = Argument(1);
            var apple = new AppleCommands(_output);
            return AppleCommands.NeedsElevation(subcommand)
                ? WithElevation(() => apple.Execute(subcommand, _args))
                : apple.Execute(subcommand, _args);
        }

        private ExitCode RunSupervisor()
        {
            using (var supervisor = new Supervisor(new HardwareContext(), stayAliveWhenIdle: false, notify: null))
            {
                supervisor.Run();
            }
            return ExitCode.Success;
        }

        private ExitCode RunSentinel()
        {
            if (!int.TryParse(Argument(1), out int supervisorProcessId))
            {
                _output.WriteLine("sentinel takes the supervisor's process id");
                return ExitCode.Usage;
            }
            new Sentinel(new HardwareContext()).Watch(supervisorProcessId);
            return ExitCode.Success;
        }

        private ExitCode SetRawGpuOffsetsCommand()
        {
            if (!int.TryParse(Argument(1), out int coreMhz) || !int.TryParse(Argument(2), out int memoryMhz))
            {
                _output.WriteLine("gpu takes two whole numbers of MHz: <core> <memory>");
                return ExitCode.Usage;
            }
            return WithElevation(() =>
            {
                var hardware = new HardwareContext();
                hardware.Performance.ApplyGpuOffsets(coreMhz, memoryMhz);
                new StatusReport(hardware).Write(_output);
                return ExitCode.Success;
            });
        }

        private ExitCode WithElevation(Func<ExitCode> command) => Elevation.IsElevated ? command() : RelaunchElevated();

        // The ACPI-WMI blocks are sometimes readable unelevated (after an elevated query enabled collection), sometimes not.
        private ExitCode WithElevationFallback(Func<ExitCode> command)
        {
            try
            {
                return command();
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied && !Elevation.IsElevated)
            {
                return RelaunchElevated();
            }
        }

        private ExitCode RelaunchElevated()
        {
            int? childExitCode = Elevation.RelaunchElevated(_args, _output);
            if (childExitCode.HasValue)
            {
                return (ExitCode)childExitCode.Value;
            }
            _output.WriteLine("Elevation was declined; nothing was changed.");
            return ExitCode.ElevationDeclined;
        }
    }
}
