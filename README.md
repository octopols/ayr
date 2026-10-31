<p align="center">
  <img src="docs/images/quick.png" alt="Ayr's tray menu" width="280">
</p>

<h1 align="center">Ayr</h1>

<p align="center"><b>Fan and performance control for MSI laptops, without MSI Center.</b><br>
A 200 KB portable tray app for the few things you actually open MSI Center for. It stays out of the way the rest of the time.</p>

---

## Why

MSI Center (formerly Dragon Center) runs a dozen background services just to switch fan and performance modes. Ayr does
those jobs directly, through the same firmware interfaces MSI's software uses, from one small executable:

- **Light.** About 0.1% CPU and 30 MB of memory when idle. Nothing polls while no window is open, and the tray runs in
  Windows' efficiency mode.
- **Self-contained.** No installer, no services, and no MSI software needed: the few MSI files Ayr relies on ship inside
  its folder.
- **Only MSI's own drivers.** Ayr loads the same two signed MSI drivers MSI Center does, and nothing else, and locks them
  so only administrators can use them.
- **Safe by default.** Every change can be rolled back, and Auto is always the fallback.

## Features

| | |
|---|---|
| **Fan modes** | Auto, Max, Silent and Boost (Cooler Boost). Silent switches back to Auto at 90 °C. |
| **Performance** | Turbo, High, Medium and Low, the same EC power profiles MSI Center uses. |
| **GPU overclock** | Core and memory offsets in Turbo, within MSI's limits for your GPU, kept across driver resets and sleep. |
| **Start with Windows** | Restores your settings at sign-in, the job MSI's services did at boot. |
| **Command line** | Everything in the tray is also available as `ayrctl`, for scripts and shortcuts. |
| **Apple fast charge** | *Experimental.* MSI Super Charger's fast-charge request for iPhones and iPads, sent automatically when plugged in or by hand, with each device's state shown. |
| **Updates** | Checks GitHub once a day and installs new versions after verifying them. Can be switched off in Settings. |

<p align="center"><img src="docs/images/main.png" alt="Ayr's main window" width="420"></p>

## Supported laptops

Ayr changes settings inside your laptop's embedded controller, so it only switches modes on models whose registers have
been verified.

✅ works, tested · 🧪 works, partly tested · ➖ the device doesn't have it · ❌ the device has it, Ayr doesn't support it yet

| Device | Type | Fan modes | Cooler Boost | Performance levels | GPU overclock | Apple fast charge | Start with Windows |
|---|---|---|---|---|---|---|---|
| MSI GF65 Thin 10SDR | Laptop | ✅ | ✅ | ✅ | ✅ | 🧪 | 🧪 |

Notes on the 🧪 entries, and on desktop motherboards, are in [docs/supported-models.md](docs/supported-models.md).
Other MSI laptops with 10th-gen Intel or older very likely work the same way but haven't been tested. 11th-gen and newer
use different firmware that Ayr doesn't support yet.

**Don't see your model?** You can check it without changing anything: `ayrctl probe` and `ayrctl status` only read.
[docs/supported-models.md](docs/supported-models.md) shows what to compare, and a
[Model report](../../issues/new/choose) is the fastest way to get your laptop added. Every report helps.

**Requirements:** Windows 10 or 11, 64-bit. .NET Framework 4.8 is built into Windows. Nothing from MSI needs to be
installed, and MSI Center should be uninstalled: it re-applies its own profile after sleep, overriding Ayr.

## Install

1. Download `Ayr-<version>-win-x64.zip` from [Releases](../../releases) and extract it anywhere, for example `C:\Tools\Ayr`.
   Keep the `vendor` folder next to `Ayr.exe`.
2. Run `Ayr.exe` and accept the administrator prompt; changing fan modes needs administrator rights. On a laptop where
   MSI's software was never installed, Ayr sets up MSI's firmware interface and asks for one restart.
3. Optional: in the main window, turn on **Start with Windows**.

To remove Ayr, run `ayrctl uninstall` (fans back to Auto, logon task and Ayr's driver entries removed), choose **Exit** in
the tray menu, and delete the folder.

## Use

**Tray:** click the icon for the quick menu; double-click it for the full window.

**Command line:**

```text
ayrctl status                      fan mode, performance level, temperatures, fan speeds, GPU offsets
ayrctl fan auto|max|silent|boost
ayrctl perf turbo|high|medium|low
ayrctl oc <core> <memory>          Turbo GPU offsets in MHz
ayrctl autostart on|off
ayrctl probe                       read-only register dump, for checking a new model
ayrctl help                        everything else
```

Settings are kept in `data\` next to the executables, and `data\events.log` records every change and every safety action.

## Safety

- **Snapshot and rollback.** Before any change, Ayr records the current fan, performance and GPU state, and restores it
  if the change fails or you press Ctrl+C.
- **Silent is always guarded.** A background guard switches to Auto if the CPU or GPU reaches 90 °C or temperatures
  can't be read, and a separate watchdog forces Auto if the guard itself stops.
- **Narrow writes.** Only the fan curve, fan mode, performance level and Cooler Boost settings, plus the one-byte
  firmware handshake MSI's service also sets. Never the BIOS, battery or SMBus.
- **Only MSI's signed files, verified.** Ayr checks the signature of every MSI file before using it, loads MSI's drivers
  only when a feature needs them, never stops them while in use, and restricts them to administrators.

How all of this works is in [docs/how-it-works.md](docs/how-it-works.md).

## FAQ

**Does it work with games that use kernel anti-cheat, like Valorant?** Ayr loads only MSI's own signed drivers, the same
ones MSI Center loads at every boot. It hasn't been confirmed in a game yet; if you try it, please say so in an issue.

**Does it replace all of MSI Center?** No. It covers fans, performance levels, the GPU overclock, and Apple fast charge.
Lighting, battery charge limits and display profiles stay with MSI Center.

**Why administrator rights?** Windows only lets administrators write the firmware settings Ayr changes. Ayr asks once,
when the tray starts; with **Start with Windows** on, it doesn't ask at all.

**Is it safe to try on my laptop?** Reading is always safe. Changing modes on an unverified model is at your own risk;
start with a probe and a model report.

## Build from source

Needs Windows and Visual Studio 2022 or the VS Build Tools (for the C# compiler).

```powershell
./scripts/build.ps1            # builds bin\Ayr.exe and bin\ayrctl.exe, and copies vendor\ next to them
./scripts/e2e-test.ps1         # end-to-end test on real hardware (elevated; changes fan and performance modes)
```

Every push is built by GitHub Actions; pushing a tag such as `v0.1.0` publishes a release.

**Layout:** `src/Ayr` holds the app: `Hardware` (EC, WMI, GPU and driver access), `Controllers` (fan and performance
logic, snapshots), `Agent` (background guard), `Apple`, `Cli`, `Ui`. `vendor/` holds the six MSI files Ayr ships with.
Documentation is in [docs/](docs/).

## Disclaimer

Ayr is an independent project. It is not affiliated with, endorsed by, or supported by Micro-Star International (MSI).
MSI, MSI Center, Dragon Center, Cooler Boost and Super Charger are trademarks of their owners and are used only to
describe compatibility. The files in `vendor/` are MSI's, unmodified, and remain MSI's property.

Ayr changes low-level hardware settings. It is provided as is, without warranty; you use it at your own risk.

## License

Copyright © 2026 Hirnaymay Bhaskar. All rights reserved. No license has been granted yet.
