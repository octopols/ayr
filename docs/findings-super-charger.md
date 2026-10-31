# MSI Super Charger: how it works and why it does nothing on this laptop

> **Lab notes.** Written during the investigation, when Ayr was still called `msictl`. Read top to bottom: the early
> sections record wrong turns, and "Route 2" near the end is what Ayr ships. The crash dump, disassembly and test logs
> cited here stayed on the investigation machine and are not part of this repository.

Super Charger 1.3.0.29 (`C:\Program Files (x86)\MSI\Super Charger\`) is an old MSI desktop-motherboard utility. It promises
faster charging for phones/tablets on USB ports and has two unrelated mechanisms. Everything here comes from **static analysis
only**: none of its executables were run and none of its drivers were loaded (it ships `NTIOLib_X64.sys`, which is not
allowed on this machine). Disassembly listings: `ipadtst2_64.asm`, `ChargeService.asm` in this folder. Tools: Capstone 5.0.7 and
`D:\projects\msi-control\tools\scripts\{pe_strings,disasm,xref,read_va}.py`.

All binaries are native: no .NET, so nothing could be decompiled to C#.

## Components

| File | Role |
|---|---|
| `Super Charger.exe`, `Super-Charger.exe` | MFC UI (two generations). Writes the choices to `ChargeSetting.ini` / `msiChargeSetting.ini` `[SYNC]` |
| `ChargeService.exe` (service `MSI_SuperCharger`, Manual, stopped) | Does the work: board detection, Super I/O programming, Apple detection, launches `ipadchg2.exe` |
| `Driver_Engine.dll` + `NTIOLib.sys` / `NTIOLib_X64.sys` (service name `NTIOLib_SuperCharger`) | Raw I/O-port access used to program the Super I/O chip (`ReadIoPortByte` / `WriteIoPortByte` / `...Word`) |
| `ipadchg.exe` + `ipadtst.sys` / `ipadtst_64.sys` | Apple "faster charging", Windows 7-era hub drivers |
| `ipadchg2.exe` + `ipadtst2.sys` / `ipadtst2_64.sys` | Apple "faster charging", Windows 8+ (`usbhub3`) and AMD (`amdhub31`) hubs |
| `Super Charger.ini` | Supported boards (`[MainBoard]`) and Apple device list (`[FASTER_CHARGING_LIST]`) |

## Mechanism 1: "Fast charging" for any device (desktop boards only)

1. `ChargeService` queries WMI `Select * from Win32_Baseboard` (`BaseBoardProduct`) and looks the board up in `[MainBoard]`.
   Entries look like `(MS-7823);0;1;24;0;0;NCT6792;`: board model, three numbers per board (pin/register parameters; exact meaning
   not decoded), and the Super I/O chip. Classes in the binary: `SuperIO`, `NCT6779D`; chip names `NCT6779D`, `NCT6792`, `NCT6795`.
2. It loads `NTIOLib_SuperCharger` and writes the Nuvoton Super I/O registers through I/O ports. A per-board `switch`
   (`ChargeService.asm` around `0x409762`, 0x4C cases) calls Super I/O methods with board-specific arguments.
3. That flips a GPIO wired to the board's USB charging-port controller, putting the port into a charge-only mode with higher
   current. That is why the UI warns "Syncing will be disabled in fast-charging mode." Options exist for enabling it in
   suspend, hibernate and shutdown (`E S3`, `AUTO_CHARGE`, `USB_LINK_ONLY`).

**On this laptop:** the board is `MS-16W1`, which is not in the 69-entry `[MainBoard]` list. A notebook has no Nuvoton Super I/O; its
EC owns those functions. The UI string `Super Charger only supports MSI mainboard!` is what you would get.

## Mechanism 2: Apple "faster charging" (`IPAD_IPHONE4_CHARGE`)

### Detection (ChargeService.exe)
- `0x4088a0` reads `HKLM\SYSTEM\CurrentControlSet\Services\WinUsb\Enum`: `Count`, then values `0`..`Count-1` (device instance paths of
  every device bound to the **WinUSB** driver). It compares them against the VID/PID lists and reports type `IPAD`.
  The exact string-compare loop was not traced instruction by instruction.
- If a device is found, `0x408180` runs `<install dir>\ipadchg2.exe` hidden with `WinExec(path, SW_HIDE)` (call sites `0x40798c` at start,
  `0x407c5d` in the polling loop). It reruns when a device newly appears, and again after 300 polling ticks
  (`cmp [0x42d608], 0x12c`). The poll interval is `LINKCHECK=3000` ms in the ini.

### The request (ipadtst2_64.sys, MSI-signed kernel driver)
- `ipadchg2.exe` installs/starts the driver and calls `DeviceIoControl` on `\\.\IpadtstSamp2`:
  - `0x9C402419` (`IOCTL_IPAD_SUPPORT_7`) → target hub driver `\Driver\usbhub3` (Windows' USB 3 hub)
  - `0x9C402421` (`IOCTL_IPAD_SUPPORT_9`) → `\Driver\amdhub31` (AMD USB 3.1 hub)
- Handler `0x140005088`: `ObReferenceObjectByName(<hub driver>)`, walks the driver's device objects (one per attached USB device),
  and for each one calls `0x140001000`.
- `0x140001000` (IsApple): URB `0x000B0088` = `URB_FUNCTION_GET_DESCRIPTOR_FROM_DEVICE`, device descriptor, 0x12 bytes, sent with
  `IOCTL_INTERNAL_USB_SUBMIT_URB` (`0x220003`). Returns `idVendor == 0x05AC` (Apple).
- For the first Apple device it builds URB `0x00170088` = **`URB_FUNCTION_VENDOR_DEVICE`**, TransferFlags 0 (host→device), no data:

  | Field | Value |
  |---|---|
  | bmRequestType | `0x40` (vendor, device, OUT): implied by the URB function and direction |
  | bRequest | `0x40` |
  | wValue | `0x6400` |
  | wIndex | `0x6400` |
  | wLength | 0 |

  (`ipadtst2_64.asm` `0x140005175`-`0x140005196`; same constants in the older `ipadtst_64.sys`.)
- So "faster charging" is **one USB vendor control request** sent to the Apple device, asking it to draw more current from the port.
  It doesn't change the port hardware. The device decides whether to honour it. Apple's semantics for `0x6400` aren't
  documented here and were not tested.

### Device list (`[FASTER_CHARGING_LIST]` in `Super Charger.ini`)

| VID/PID | Name in the ini |
|---|---|
| `05AC:129A` | Apple iPad |
| `05AC:1297` | Apple iPhone 4 |
| `05AC:129F` | Apple iPad 2 |
| `05AC:12A0` | Apple iPhone 4S |
| `05AC:12A4` | Apple New iPad Wi-Fi |
| `05AC:12A6` | Apple New iPad Wi-Fi + 4G |
| `05AC:12AB` | Apple New iPad 4th gen and iPad mini |
| `05AC:12A8` | Apple iPhone 5/6 |

`Super-Charger.INI` (older UI) has the first six only. Note that `05AC:12A8` is the PID that every iPhone since the 5 reports, so in
practice "iPhone 5/6" means any modern iPhone.

The kernel driver itself doesn't use the list; it accepts **any** device with vendor ID `0x05AC`. The list only matters
for detection in ChargeService.

### On this laptop
- Two iPhones have been connected before (`USB\VID_05AC&PID_12A8\00008110001A7DCE2100A01E`, last seen 2026-07-07, and
  `...\00008120000C489221C3A01E`, last seen 2026-10-02). Both bind to `usbccgp` + `WUDFWpdMtp` (Windows' MTP driver), **not WinUSB**.
  `WinUsb\Enum` is empty, so ChargeService's detection would never fire for them.
- Service `MSI_SuperCharger` is Manual and stopped. `ChargeSetting.ini` is all `na` (never configured). The `ipadtst2` driver service
  exists but is stopped.
- Conclusion: **Super Charger has no effect on this laptop**, and its hardware path depends on `NTIOLib`, a known-vulnerable driver.

## Could msictl do the Apple request instead?
Not under the current rules. The request has to reach the iPhone's USB device. With the phone on `usbccgp`/MTP, user mode can't
send a raw vendor request to it. MSI does it from a kernel driver (`ipadtst2_64.sys`), and the alternative would be rebinding the
phone to WinUSB, which breaks Photos/MTP access. Both mean loading or installing a driver that isn't loaded today. Modern iPhones also
negotiate charging current on their own, and whether they still honour this request is untested.

## Correction and incident (2026-10-31, evening)

**Correction.** With an iPhone actually connected, `WinUsb\Enum` lists its MTP interface (`USB\VID_05AC&PID_12A8&MI_00\...`).
Windows' MTP driver stack is `WpdUpFltr → WudfRd → WINUSB → usbccgp`, so WinUSB is in it. The phone hangs off a `USBHUB3` root hub
(port `\_SB.PCI0.XHC.RHUB.HS01`). So Super Charger's detection **does** see it, and MSI's IOCTL `0x9C402419` targets the right hub
driver. The earlier statement that detection would never fire was wrong: the key was only empty because no phone was connected.
The service was set from Auto to Manual on 2026-08-31 02:06 (System log 7040), so until then Super Charger ran at every boot.

**Incident.** msictl briefly gained `apple boost/enable`, which sent the same IOCTL through `ipadtst2_64.sys` exactly as
`ipadchg2.exe` does. During the live test (`apple-test-result.txt`) two requests at 22:02:05 and 22:02:06 returned normally. The
supervisor's watcher would have sent a third about 3 s later. **The PC then froze completely** and needed a hard reset (Kernel-Power 41,
event 6008 "previous shutdown was unexpected", no crash dump: a kernel-side hang, not a bugcheck). On the next boot the iPhone's MTP
driver failed once (`WudfRd`, `0xC0000365`), then recovered.

Likely cause (not proven): the driver sends a synchronous `GET_DESCRIPTOR` URB to **every** device object owned by `\Driver\usbhub3`,
including hub FDOs and non-Apple devices, and waits on each without a timeout (`KeWaitForSingleObject` after `IofCallDriver` returns
`STATUS_PENDING`). On this Windows 11 26200 USB stack, one of those waits never completes. Back-to-back requests make it worse.

**Consequence.** The send path was removed from msictl entirely: there is no code left that loads `ipadtst2` or opens `\\.\IpadtstSamp2`.
What remains is read-only: `msictl apple`, `msictl apple list [--all]`, and `msictl apple unload` (stops the driver if something else
loaded it). Re-enabling the Super Charger service would put this same driver back in play, so that isn't recommended either.

## Second incident: crash after a single request (2026-10-31 22:43)

To rule out "too many requests", msictl was rebuilt to copy `ipadchg2.exe` exactly: one handle, IOCTL `0x9C402419`, 400 ms pause,
IOCTL `0x9C402421`, 100 ms pause, the same `"\x03\0"` input and 5-byte output. It was rate-limited to Super Charger's real cadence:
the service loop is `Sleep(900)` × 300 ticks ≈ 5 min (`ChargeService.asm` `0x4079a3`, `0x407c33`).
`charge-test-result.txt`:
- 22:43:27: **one** request sent; it returned normally and the driver was loaded.
- 22:43:27 to 22:43:59: the system ran normally for 30 s.
- ~22:44: the driver was stopped (unloaded) and the PC **bugchecked** (black "kernel" stop screen), then rebooted.

Minidump `103126-28640-01.dmp` (copied to this folder; analysis in `crash-103126.txt`, cdb from WinDbg 1.2610):
- `0x139` KERNEL_SECURITY_CHECK_FAILURE, Arg1 = 3: **FAST_FAIL_CORRUPT_LIST_ENTRY** (a corrupted doubly-linked list).
- Detected in `igdkmd64.sys+0x3c27e` (Intel graphics) → `RtlQueryRegistryValuesEx` → `NtOpenKey`. That's an innocent registry read that ran into an
  already-corrupted kernel list. The bucket names the victim, not the culprit.
- `ipadtst2_64.sys` was still in the loaded-module list at the moment of the crash.

**Conclusion:** both incidents followed directly from exercising this driver: a hang after a burst of requests (no unload), and
kernel list corruption detected during unload after a single request. Its code has the flaws listed above: an unlocked walk of another
driver's device list, infinite waits, no status checks, and a leaked object reference on every call. The driver is v2.0.0.0 from Feb 2020.
This PC runs Windows 11 25H2 (26200.8655) with `USBHUB3.SYS` from May 2026. It may well have worked on the Windows builds of its time,
which fits fast charge working until Aug 31, but **it is not safe on this system**.

msictl no longer contains any code that loads, calls or unloads `ipadtst2`. `msictl apple` / `apple list` are read-only. Re-enabling
Super Charger's own service would load the same driver, so that isn't recommended either.

## Third test: MSI's own helper, driver never unloaded (2026-10-31 23:10)

At the user's choice ("Route 2"), msictl now runs MSI's **unmodified `ipadchg2.exe`** from Super Charger's folder, as ChargeService did.
It does this once per plug-in (after 15 s settle), never more often than every 5 minutes, and **never stops the driver**.
`charge-test2-result.txt`: `ipadchg2` exit 0 ("IOCTL_IPAD_SUPPORT_7 IOCTL_IPAD_SUPPORT_9"). The driver stayed loaded and the iPhone stayed
connected (no re-enumeration). The system was stable for the 2 min observed. The charging-speed effect is **not yet verified**: the phone
was at 100 %.

## Fn keys (measured 2026-10-31 with `msictl keys learn`, `data\fnkeys.txt`)

Only two Fn keys raise the firmware `MSI_Event`. The rest (F1–F6, F8–F11, backlight) are handled by the EC and Windows with no MSI software:

| Key | MSI_Event | MSI's handling |
|---|---|---|
| Fn+F7 | `0x220046`, code 70 "DeviceKey" | `OmApSvcBroker` / DCv2 launch Dragon Center |
| Fn+F12 | `0x22003F`, code 63 (repeats while held) | not handled by any MSI component |
| Cooler Boost key | code 4 | EC toggles `0x98` bit 7 itself; DCv2 only syncs its UI |

Fn+F7 is left unassigned for now; the user will decide its action when the msictl UI is built.

## Recommendation
Uninstall Super-Charger (listed as "Super-Charger 1.2.016" in Programs). It does nothing here, and it removes `NTIOLib_X64.sys`,
`ipadtst*_64.sys` and a stopped service from disk. Ask before doing it: it's the same go/no-go as the Dragon Center removal.
