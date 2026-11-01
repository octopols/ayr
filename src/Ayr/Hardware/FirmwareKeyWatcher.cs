using System;
using System.Collections.Generic;
using System.Management;

namespace Ayr.Hardware
{
    /// <summary>
    /// The Fn keys the firmware hands to software, reported through MSI_Event, whose low byte is the event code. On the
    /// GF65 only Fn+F7 and Fn+F12 do this; every other Fn key (backlight, Cooler Boost, volume) is handled by the laptop
    /// itself. The firmware sends a burst of events per press, and Fn+F12 repeats while held, so each key reports once
    /// per press.
    /// </summary>
    public sealed class FirmwareKeyWatcher : IDisposable
    {
        private const int EventCodeMask = 0xFF;
        private static readonly TimeSpan RepeatWindow = TimeSpan.FromMilliseconds(600);

        private static readonly Dictionary<int, FnKey> KeyCodes = new Dictionary<int, FnKey>
        {
            [70] = FnKey.F7,
            [63] = FnKey.F12
        };

        private readonly ManagementEventWatcher _watcher =
            new ManagementEventWatcher(new ManagementScope(@"\\.\root\WMI"), new WqlEventQuery("MSI_Event"));
        private readonly Dictionary<FnKey, DateTime> _lastEvent = new Dictionary<FnKey, DateTime>();
        private readonly object _gate = new object();

        public FirmwareKeyWatcher()
        {
            _watcher.EventArrived += OnEventArrived;
            _watcher.Start();
        }

        /// <summary>Raised on a WMI thread, once per press.</summary>
        public event Action<FnKey> Pressed;

        public void Dispose()
        {
            _watcher.Stop();
            _watcher.Dispose();
        }

        private void OnEventArrived(object sender, EventArrivedEventArgs e)
        {
            int code = Convert.ToInt32(e.NewEvent["MSIEvt"]) & EventCodeMask;
            if (KeyCodes.TryGetValue(code, out FnKey key) && IsNewPress(key))
            {
                Pressed?.Invoke(key);
            }
        }

        // Events closer together than the window belong to the same press; holding the key keeps extending it.
        private bool IsNewPress(FnKey key)
        {
            lock (_gate)
            {
                DateTime now = DateTime.UtcNow;
                bool isNew = !_lastEvent.TryGetValue(key, out DateTime last) || now - last > RepeatWindow;
                _lastEvent[key] = now;
                return isNew;
            }
        }
    }
}
