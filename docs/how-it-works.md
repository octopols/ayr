# How Ayr works

MSI laptops expose their fan and performance controls through firmware interfaces that MSI Center (formerly Dragon
Center) talks to. Ayr talks to the same interfaces directly, so it needs none of MSI Center's services. This page explains
those interfaces, what Ayr changes through each, and how it keeps that safe.

Values marked *reference* were measured on the first verified model, the GF65 Thin 10SDR. Other models may differ; see
[supported-models.md](supported-models.md).

## The two firmware generations

MSI's own hardware library has two code paths, and every MSI laptop uses one of them:

| | Older firmware ("v1") | Newer firmware ("v2") |
|---|---|---|
| Platforms | 10th-gen Intel and earlier (pre-Tiger Lake) | 11th-gen Intel (Tiger Lake) and newer |
| How settings are reached | ACPI-WMI data blocks (`MSI_System`, `MSI_CPU`, …), indexed by instance | `MSI_ACPI` WMI methods (`Get_Data` / `Set_Data`) with raw EC addresses |
| How to tell | `HKLM\SOFTWARE\WOW6432Node\MSI\NB\WMIEC` is empty | that registry value is filled in |

**Ayr currently implements the v1 path only.** v2 support is planned; see [supported-models.md](supported-models.md).

## Where each setting lives (v1)

The embedded controller (EC) holds the live settings. Part of its RAM is mirrored into ACPI-WMI blocks that Windows'
own `wmiacpi.sys` driver exposes: `MSI_System[i]` is EC RAM `0xEB + i`.

| Control | Register | Values |
|---|---|---|
| Performance level | `MSI_System[7]` = EC `0xF2` | Turbo `0xC4`, High `0xC0`, Medium `0xC1`, Low `0xC2` |
| Fan mode | `MSI_System[9]` = EC `0xF4` | bit 7 Advanced, bit 6 Basic, bit 4 Silent; all clear is Auto |
| Fan curve | `MSI_CPU[11..17]`, `MSI_VGA[11..17]` | fan duty (%) for each temperature step |
| Cooler Boost | EC `0x98`, bit 7 | not mirrored in any WMI block |
| Temperatures | `MSI_CPU[1]`, `MSI_VGA[1]` | °C |
| Fan duty | `MSI_CPU[2]`, `MSI_VGA[2]` | % |
| Fan speed | `MSI_AP[2..3]` CPU, `MSI_AP[4..5]` GPU | a period, low byte first: RPM = 60,000,000 / (period × 2 × 62.5) |

The performance level only changes the EC's power profile; the actual power limits live inside the EC and BIOS. On the
reference model MSI Center changes nothing else for it either (no power plan or CPU limit changes; its CPU overclock is
only offered on GT-series models).

### Fan modes

| Ayr mode | Fan curve | Fan-mode byte | Cooler Boost bit | MSI Center equivalent |
|---|---|---|---|---|
| Auto | the model's stock curve | Advanced, Basic and Silent bits clear | off | Auto |
| Max | every point at 100% | Advanced bit set | off | Advanced, all points at 100 |
| Silent | stock curve | Silent bit set | off | the Silent scenario |
| Boost | stock curve | bits clear | on | Cooler Boost |

Cooler Boost runs the fans above their normal 100% speed (*reference*: about 20-40% faster). The Cooler Boost key on the
keyboard toggles the same bit in hardware, and Ayr never overrides it.

### Reaching EC RAM

Cooler Boost has no WMI block, so Ayr writes EC RAM directly. On v1 firmware, EC RAM is memory-mapped at a physical
address (*reference*: `0xFC000800`). The only safe way to touch it is a driver that is already installed and running, and
MSI ships one: the WINIO driver (`KernCoreLib64.sys`) that MSI Foundation Service uses. Ayr reaches it two ways:

- **Elevated:** in-process through MSI's `KernCoreLib64.dll`, the same calls MSI Foundation Service makes.
- **Not elevated:** through MSI Foundation Service's named pipe `MSI_SERVICE_2`, which any signed-in user may use.

Both only support 32-bit access, so writing one byte reads the surrounding four, changes one, and writes them back, as
MSI's own code does.

### GPU overclock

In Turbo, MSI Center applies GPU core and memory clock offsets through `GInf.dll`, MSI's wrapper around NVIDIA's NVAPI,
which ships with MSI Foundation Service. Ayr calls the same library. Offsets are in MHz, are lost on reboot or a driver
reset, and are limited to the range MSI Center allows for the GPU (*reference*: GTX 1660 Ti, core 0-180, memory 0-500).

## What MSI's services did, and what replaces them

| MSI component | What it did for fans and performance | In Ayr |
|---|---|---|
| MSI Center / Dragon Center UI | Sent mode changes to MSI's central service | Ayr's tray and window |
| MSI Central Service (MSI SDK) | Applied scenarios; re-applied its own scenario after every resume | Ayr's background guard re-applies *your* settings after resume |
| MSI Foundation Service | Sets a firmware handshake at boot; EC access pipe; `GInf.dll` | Ayr sets the same handshake (`MSI_Software[0] = 1`) and uses the libraries directly. **Keep it installed.** |
| Micro Star SCM | Fn-key handling and a WMI proxy | Not needed for fans and performance |

## Safety design

- **Snapshot and rollback.** Before every change Ayr records the fan curve, fan-mode byte, Cooler Boost bit, performance
  level and GPU offsets (`data\snapshots.log`). If the change fails or you press Ctrl+C, the snapshot is restored.
- **Silent is never unguarded.** A background guard switches to Auto when the CPU or GPU reaches 90 °C, or when
  temperatures can't be read three times in a row. A separate watchdog process waits on the guard and forces Auto if
  the guard dies, even if it is killed. A failed change out of Silent lands on Auto rather than back on Silent.
- **Allow-listed writes.** Only `MSI_CPU`, `MSI_VGA`, `MSI_System`, the handshake byte in `MSI_Software`, and the
  Cooler Boost register. Battery, device and power blocks are read-only, and Ayr never writes the BIOS or SMBus.
- **No new drivers.** Ayr uses `wmiacpi.sys` (part of Windows) and MSI's already-running WINIO driver. That is also what
  keeps it out of the way of kernel anti-cheat.
- **Never fights other changes.** If something else changes a mode, for example the Cooler Boost key, Ayr leaves it.

## Testing

`scripts/e2e-test.ps1` runs on real hardware (elevated). It switches every fan mode and performance level, applies and
checks GPU offsets, kills the guard to prove the watchdog restores Auto, lowers the Silent limit for the test to prove the
guard trips, and wipes the GPU offsets to prove the guard reapplies them. It ends with the fans on Boost and the
performance level on Turbo.
