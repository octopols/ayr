<p align="center">
  <img src="docs/images/quick.png" alt="Ayr's tray menu" width="280">
</p>

<h1 align="center">Ayr</h1>

<p align="center"><b>Fan and performance control for MSI laptops, without MSI Center.</b><br>
A 200 KB portable tray app that does the few things you actually opened Dragon Center for, and otherwise stays out of the way.</p>

---

## Why

MSI Center / Dragon Center ships a dozen background services to switch fan and performance modes. Ayr does those jobs
directly, the same way MSI's software does them, from one small executable:

- **Light.** About 0.1% CPU and 28 MB at idle. Nothing polls while no window is open, and the tray runs in Windows'
  efficiency mode.
- **Portable.** No installer and no services: extract it, run it, delete the folder to remove it.
- **No new drivers.** Ayr only uses what MSI and Windows already loaded, which is what keeps it compatible with
  kernel anti-cheat.
- **Safe by default.** Every change can be rolled back, and Auto is always the fallback.

## Features

| | |
|---|---|
| **Fan modes** | Auto, Max, Silent and Boost (Cooler Boost). Silent switches back to Auto at 90 °C. |
| **Performance** | Turbo, High, Medium, Low, the same EC modes MSI uses. |
| **GPU overclock** | Core and memory offsets applied in Turbo, within MSI's own limits, kept across driver resets and sleep. |
| **Apple fast charge** | One fast-charge request when an iPhone or iPad is plugged in, through MSI Super Charger's own helper. |
| **Start with Windows** | Restores your settings at sign-in, replacing what MSI's services did at boot. |
| **Command line** | Everything in the tray is also available as `ayrctl`. |

<p align="center"><img src="docs/images/main.png" alt="Ayr's main window" width="420"></p>

## Will it work on my laptop?

> [!WARNING]
> Ayr has been tested on **one model only: the MSI GF65 Thin 10SDR (MS-16W1, BIOS E16W1IMS.50C)**.
> It writes to the embedded controller using that model's register map. Other MSI laptops from the same generation
> (10th-gen Intel, pre-Tiger Lake firmware) probably use the same map, but that is not verified. On anything else, don't
> run it.

Before switching a mode on an untested model, run `ayrctl probe`. It only reads, and you can compare its output with the
register map in [docs/findings-dragon-center.md](docs/findings-dragon-center.md). Please
[open an issue](../../issues/new/choose) with the result either way.

**Requirements**

- Windows 10 or 11, 64-bit. .NET Framework 4.8 is built into Windows, so there's nothing to install.
- **MSI Foundation Service** stays installed: Ayr uses its EC access library and its GPU library (`GInf.dll`). The rest
  of MSI Center can go.
- **MSI Super Charger**, only if you want Apple fast charge.

## Install

1. Download `Ayr-<version>-win-x64.zip` from [Releases](../../releases) and extract it anywhere, e.g. `C:\Tools\Ayr`.
2. Run `Ayr.exe` and accept the administrator prompt; changing fan modes needs administrator rights.
3. Optional: in the main window, turn on **Start with Windows**.

To remove Ayr, turn off **Start with Windows**, choose **Exit** in the tray menu, and delete the folder.

## Use

**Tray:** click the icon for the quick menu, double-click it for the full window.

**Command line:**

```text
ayrctl status                      fan mode, performance level, temperatures, fan speeds, GPU offsets
ayrctl fan auto|max|silent|boost
ayrctl perf turbo|high|medium|low
ayrctl oc <core> <memory>          Turbo GPU offsets in MHz (core 0-180, memory 0-500)
ayrctl autostart on|off
ayrctl apple on|off                Apple fast charge on plug-in
ayrctl help                        everything else
```

Settings live in `data\` next to the executables; `data\events.log` records every change and every safety action.

## Safety

- **Snapshot and rollback.** Before any write, Ayr records the current fan, performance and GPU state, and restores it if
  the change fails or you press Ctrl+C.
- **Silent is guarded.** A background guard switches to Auto if the CPU or GPU reaches 90 °C or temperatures can't be
  read. A separate watchdog process forces Auto if the guard itself dies.
- **Narrow writes.** Only the fan curve, fan mode, performance mode and Cooler Boost registers, plus a one-byte firmware
  handshake. Never the BIOS, battery or SMBus.
- **No new drivers.** Ayr uses the Windows ACPI-WMI driver and MSI's already-installed WINIO driver; it never loads
  anything of its own.

## FAQ

**Does it work with Valorant / Riot Vanguard?** It's designed to, since Ayr loads no driver and uses only components
that are already running, but this hasn't been confirmed in a game yet. If a game is unstable in Turbo, set both GPU
offsets to 0 or use High.

**Does it replace all of MSI Center?** No. It covers fans, performance modes, the GPU overclock and Apple charging.
Lighting, battery charge limits and display profiles aren't included.

**Why administrator rights?** Fan and performance writes go through ACPI-WMI and the EC, which Windows only allows for
administrators. Ayr asks once, when the tray starts.

## Build from source

Needs Visual Studio 2022 or the VS Build Tools (for the C# compiler) on Windows.

```powershell
./scripts/build.ps1            # builds bin\Ayr.exe and bin\ayrctl.exe
./scripts/deploy.ps1           # copies them to D:\Portable\Ayr and adds a Start menu shortcut
./scripts/e2e-test.ps1         # end-to-end test on real hardware (elevated; changes fan and performance modes)
```

Pushing a tag such as `v0.1.0` builds and publishes a release through GitHub Actions.

**Layout:** `src/Ayr` holds the app: `Hardware` (EC, WMI, GPU access), `Controllers` (fan and performance logic,
snapshots), `Agent` (background guard), `Apple`, `Cli`, `Ui`. How the hardware was mapped, step by step, is in
[docs/](docs/).

## Disclaimer

Ayr is an independent project. It is not affiliated with, endorsed by, or supported by Micro-Star International (MSI).
MSI, Dragon Center, MSI Center, Cooler Boost and Super Charger are trademarks of their owners and are used here only to
describe compatibility.

Ayr changes low-level hardware settings. It is provided as is, without warranty; you use it at your own risk.

## License

Copyright © 2026 Hirnaymay Bhaskar. All rights reserved. No license has been granted yet.
