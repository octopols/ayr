using System;
using System.Management;

namespace Ayr.Hardware
{
    /// <summary>
    /// The one Fn key the firmware hands to software: Fn+F7, which MSI's apps used to open Dragon Center. Every other Fn
    /// key (backlight, Cooler Boost, volume) is handled by the laptop itself and needs nothing from Ayr. The firmware
    /// raises MSI_Event, whose low byte is the event code.
    /// </summary>
    public sealed class FirmwareKeyWatcher : IDisposable
    {
        private const int OpenAppKeyCode = 70;
        private const int EventCodeMask = 0xFF;

        private readonly ManagementEventWatcher _watcher =
            new ManagementEventWatcher(new ManagementScope(@"\\.\root\WMI"), new WqlEventQuery("MSI_Event"));

        public FirmwareKeyWatcher()
        {
            _watcher.EventArrived += OnEventArrived;
            _watcher.Start();
        }

        /// <summary>Raised on a WMI thread when Fn+F7 is pressed.</summary>
        public event Action OpenAppPressed;

        public void Dispose()
        {
            _watcher.Stop();
            _watcher.Dispose();
        }

        private void OnEventArrived(object sender, EventArrivedEventArgs e)
        {
            int code = Convert.ToInt32(e.NewEvent["MSIEvt"]) & EventCodeMask;
            if (code == OpenAppKeyCode)
            {
                OpenAppPressed?.Invoke();
            }
        }
    }
}
