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

- Ayr runs MSI's unmodified `ipadchg2.exe` from Super Charger's install folder, exactly as Super Charger's service did.
  Super Charger must be installed; Ayr bundles none of its files.
- One request per plug-in, sent 15 seconds after the device connects, once USB enumeration has settled.
- At most one request every 5 minutes, system-wide, which was Super Charger's own cadence.
- **The driver is never stopped.** It stays loaded until the next reboot, as it did with Super Charger.

## Why so careful

The driver walks another driver's device list without locking it, waits without timeouts, and leaks a reference on every
call. On Windows 11 25H2, testing showed:

- two requests one second apart froze the PC, and
- stopping the driver after a single request crashed it (bugcheck `0x139`, corrupted kernel list).

A single request with the driver left loaded was stable, so that is the only way Ayr uses it.

## Turning it on

In the main window, switch on **Apple fast charge**, or run `ayrctl apple on`. `ayrctl apple` shows the state, and
`ayrctl apple list` shows connected Apple devices and whether their USB hub is supported.
