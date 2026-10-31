# Apple fast charge

MSI Super Charger promised faster charging for iPhones and iPads on MSI PCs. Ayr can trigger the same request, through
Super Charger's own helper. This page explains what the request is and why Ayr handles it so cautiously.

> [!CAUTION]
> This feature relies on a 2020 MSI kernel driver that has real bugs on current Windows builds. Ayr only ever uses it in
> the one way that has been seen to be stable. It's off by default, and whether it still speeds up charging on modern
> iPhones hasn't been measured.

## What the request is

Super Charger's helper `ipadchg2.exe` starts MSI's `ipadtst2` kernel driver and asks it to send **one USB vendor control
request** (`bRequest 0x40`, `wValue 0x6400`, `wIndex 0x6400`) to the first Apple device (USB vendor `05AC`) on the
`usbhub3` or `amdhub31` hub driver. The request asks the device to draw more current. It doesn't change the port, and the
device decides whether to honour it.

Super Charger's desktop "fast charging" for all devices is a different mechanism: it programs a motherboard Super I/O
chip and does nothing on laptops.

## How Ayr uses it

- Ayr ships MSI's unmodified `ipadchg2.exe` and `ipadtst2_64.sys`, so Super Charger doesn't need to be installed. Ayr
  starts the driver itself and restricts it to administrators, then runs MSI's helper to send the request.
- While Super Charger's own service is running, Ayr sends nothing: two senders could put requests seconds apart.
- One request per device per connection. The request lasts until the device is unplugged, so Ayr counts a device as
  fast charging when a request was sent after it was last plugged in, and never sends it a second one.
- Nothing is sent until 15 seconds after the device connects, once USB enumeration has settled.
- At most one request every 5 minutes, system-wide, which was Super Charger's own cadence.
- With several Apple devices connected, a request reaches only the first one MSI's driver finds, and there is no way to
  tell which one that is.
- **The driver is never stopped.** It stays loaded until the next reboot, as it did with Super Charger.

## Why so careful

The driver walks another driver's device list without locking it, waits without timeouts, and leaks a reference on every
call. On Windows 11 25H2, testing showed:

- two requests one second apart froze the PC, and
- stopping the driver after a single request crashed it (bugcheck `0x139`, corrupted kernel list).

A single request with the driver left loaded was stable, so that is the only way Ayr uses it.

## Using it

The **Apple fast charge** card in the main window lists the connected devices. A device that has had its request shows
a green **Fast charging** badge until it is unplugged.

- **Send automatically** on: every device gets the request when it is plugged in, and switching it on also sends to
  devices already connected. Same as `ayrctl apple on`.
- **Send automatically** off: each device has a **Send** button. It is greyed out, with the reason on hover, while the
  device is still connecting, during the 5-minute gap, or once the request has been sent. Unplug and replug the device
  to send again. Same as `ayrctl apple send`.

`ayrctl apple` shows each device's state, and `ayrctl apple list` shows device details and whether the USB hub is
supported.
