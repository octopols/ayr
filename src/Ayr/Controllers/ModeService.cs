using System;
using Ayr.App;

namespace Ayr.Controllers
{
    /// <summary>
    /// Applies a fan mode or performance level as a transaction: snapshot, apply, and on any error or Ctrl+C put the
    /// snapshot back. Silent is never restored without a guard running, so a failed change out of Silent lands on Auto.
    /// </summary>
    public sealed class ModeService
    {
        private readonly HardwareContext _hardware;
        private volatile bool _cancelRequested;

        public ModeService(HardwareContext hardware)
        {
            _hardware = hardware;
        }

        public void ApplyFan(FanMode mode)
        {
            RunTransaction(includeGpu: false, apply: () => _hardware.Fans.Apply(mode));
            DesiredState.Update(state => state.Fan = mode);
            ActivityLog.Write("fan -> " + mode);
        }

        public void ApplyPerformance(PerformanceLevel level)
        {
            GpuOffsets offsets = DesiredState.Load().TurboOffsets;
            RunTransaction(includeGpu: true, apply: () => _hardware.Performance.Apply(level, offsets));
            DesiredState.Update(state => state.Performance = level);
            ActivityLog.Write("perf -> " + level + (level == PerformanceLevel.Turbo ? " (" + offsets + ")" : ""));
        }

        /// <summary>
        /// Saves the Turbo GPU offsets and, like Dragon Center's OC window, applies them at once when Turbo is active.
        /// Returns whether they were applied.
        /// </summary>
        public bool SetTurboOffsets(GpuOffsets offsets)
        {
            // Saved before applying: the supervisor compares live offsets with the saved ones and would otherwise
            // "repair" a fresh change back to the old values in the gap between apply and save.
            GpuOffsets previous = DesiredState.Load().TurboOffsets;
            DesiredState.Update(state => state.TurboOffsets = offsets);

            bool turboActive = _hardware.Performance.ReadLevel() == PerformanceLevel.Turbo;
            if (turboActive)
            {
                try
                {
                    RunTransaction(includeGpu: true, apply: () => _hardware.Performance.ApplyGpuOffsets(offsets));
                }
                catch
                {
                    DesiredState.Update(state => state.TurboOffsets = previous);
                    throw;
                }
            }
            ActivityLog.Write("turbo offsets -> " + offsets + (turboActive ? " (applied)" : " (saved for Turbo)"));
            return turboActive;
        }

        /// <summary>Puts the fans on firmware Auto. Every safety path uses this, so it must not depend on saved state.</summary>
        public void FailsafeAuto(string reason)
        {
            ActivityLog.Write("FAILSAFE auto: " + reason);
            _hardware.Fans.Apply(FanMode.Auto);
            DesiredState.Update(state => state.Fan = FanMode.Auto);
        }

        private void RunTransaction(bool includeGpu, Action apply)
        {
            StateSnapshot snapshot = StateSnapshot.Capture(_hardware.Fans, _hardware.Performance, includeGpu);
            snapshot.AppendToLog();
            _cancelRequested = false;
            Console.CancelKeyPress += OnCancelKeyPress;
            try
            {
                apply();
                if (_cancelRequested)
                {
                    throw new OperationCanceledException("Cancelled by Ctrl+C.");
                }
            }
            catch (Exception ex)
            {
                ActivityLog.Write("apply failed (" + ex.Message + "); restoring snapshot");
                RestoreAfterFailure(snapshot);
                throw;
            }
            finally
            {
                Console.CancelKeyPress -= OnCancelKeyPress;
            }
        }

        private void RestoreAfterFailure(StateSnapshot snapshot)
        {
            try
            {
                snapshot.Restore(_hardware.Fans, _hardware.Performance);
                if (snapshot.WasSilent)
                {
                    FailsafeAuto("previous state was Silent; not restoring it unguarded");
                }
            }
            catch (Exception restoreError)
            {
                ActivityLog.Write("snapshot restore failed: " + restoreError.Message);
                FailsafeAuto("snapshot restore failed");
            }
        }

        // Ctrl+C lets the in-flight write finish and then rolls back, so a curve is never left half written.
        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _cancelRequested = true;
        }
    }
}
