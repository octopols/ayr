# MSI GF65 Thin 10SDR: how Dragon Center controls fans and performance

> **Lab notes.** Written during the investigation, when Ayr was still called `msictl`. The decompiled sources, test logs
> and register dumps cited below stayed on the investigation machine and are not part of this repository.

This document records how MSI Dragon Center actually changes fan mode, Cooler Boost, performance level and GPU
clock offsets on this laptop. It is the evidence behind Ayr (then `msictl`), the small tool
that replaces Dragon Center for those controls only. Every entry below cites either decompiled Dragon Center code
or a measured before/after on this machine. Nothing here is taken from community projects without confirmation.

- Machine: MSI GF65 Thin 10SDR, board MS-16W1, BIOS E16W1IMS.50C, EC firmware `16W1EMS2.103` (from `MSI_Software` bytes 6..17)
- CPU i5-10500H (10th gen), GPU GTX 1660 Ti laptop, driver 610.62
- Dragon Center 2.0.156.0, MSI SDK 2.2026.0616.01, MSI NBFoundation Service 1.0.2411.1201
- Investigated 2026-10-31

Decompiled with ILSpy 11.2 (`ilspycmd`); source paths below are relative to that output.
Live test logs: `livetest-result.txt` (raw register experiments), `e2e-result.txt` (the shipped tool, 19/19 checks passed),
`probe-elevated.txt` (read-only dump).

---

## 1. Summary

| Control | What changes | How it is reached | Admin? |
|---|---|---|---|
| Performance level | EC `0xF2` (= WMI `MSI_System[7]`): Turbo `0xC4`, High `0xC0`, Medium `0xC1`, Low `0xC2` | ACPI-WMI `MSI_System` instance 7 | yes |
| Fan mode | EC `0xF4` (= WMI `MSI_System[9]`): bit 7 Advanced, bit 6 Basic, bit 4 Silent; all clear = Auto | ACPI-WMI `MSI_System` instance 9 | yes |
| Fan curve | `MSI_CPU[11..17]`, `MSI_VGA[11..17]` duty % per temperature step | ACPI-WMI | yes |
| Cooler Boost | EC `0x98` bit 7 | MSI Foundation Service pipe `MSI_SERVICE_2` (`IO:WMREQ`) | no |
| GPU offsets (Turbo) | NVAPI P-state 2.0 core / memory clock delta, MHz | MSI `GInf.dll` `SetGPUOC` / `SetMemoryOC` | yes |
| Telemetry | temps `MSI_CPU[1]`/`MSI_VGA[1]`, duty `[2]`, fan tach `MSI_AP[2..5]` | ACPI-WMI | usually no |

What each performance level does on this machine: **only the EC shift mode byte changes**, plus the GPU offsets in Turbo
(when the user has set them). Dragon Center's CPU overclock path (Intel XTU) is gated to GT-series laptops with specific
i9/K CPUs and never runs on an i5-10500H (§4.3). No power plan, PL1/PL2 or MSR writes are tied to the level.

---

## 2. Why the "v1" path applies to this laptop

Dragon Center's hardware library `MSIWMIACPI2.dll` (`WmiAcpiLib2`) has two code paths:

- **v2**, used when the firmware reports WMI major version ≥ 2 on a Tiger Lake (11th gen) or newer platform. It calls
  `MSI_ACPI` WMI methods (`Get_Data`/`Set_Data`, etc.) with raw EC addresses.
- **v1**, used for everything else. It reads and writes the instance-indexed ACPI-WMI data blocks (`MSI_System`, `MSI_CPU`, ...)
  directly, and uses MSI's EC-memory pipe for registers that have no block.

Evidence that this machine is on v1:

- `One_Dragon_Center/MSIWMIACPI2.dll/MSIWMIACPI2/WmiAcpiLib2.cs:466` logs "WMIACPI2 only supports Intel TigerLake(11th gen.) or newer".
- `WmiAcpiLib2.cs:607`: the registry value `HKLM\SOFTWARE\WOW6432Node\MSI\NB\WMIEC` is filled in only when `WmiMajorVersion >= 2 && IsTigerLake`.
  **Measured:** it is empty on this machine.
- This also explains the prior research result that `MSI_ACPI` is a dead end. It is the v2 interface, and this firmware doesn't implement it.

So every `if (_wmiAcpiLib2.WmiMajorVersion >= 2) { ... } else { ... }` in Dragon Center runs its `else` branch here.

---

## 3. Register mapping: WMI blocks ↔ EC RAM

**`MSI_System[i]` is EC RAM `0xEB + i`.** Measured by reading both sides with nothing changing in between (`probe-elevated.txt`):

| WMI | value | EC (via `MSI_SERVICE_2`) | value |
|---|---|---|---|
| `MSI_System[7]` | 196 | `0xF2` | `0xC4` |
| `MSI_System[8]` | 131 | `0xF3` | `0x83` |
| `MSI_System[9]` | 13 | `0xF4` | `0x0D` |

Dragon Center's own v1 code agrees: it reads the fan-mode byte as `MSI_System` instance 9 in `Auto_Fan` and as EC memory
`0xFC0008F4` (= base `0xFC000800` + `0xF4`) in `init`/`SetScenarioMode` (§5.1).

Prior-research correction: `MSI_System[19..20]` = `226,7` (= 2018 little-endian, EC `0xFE..0xFF`) is **not** fan RPM.
Dragon Center reads RPM from `MSI_AP` (§7).

---

## 4. Performance level (Turbo / High / Medium / Low)

### 4.1 UI → value
`DCv2/UC_NB_Base_Module.dll/UC_Dynamic/UC_User_Scenario.xaml:1121-1124`: the Performance combo box items in order are
Turbo, High, Medium, Low. `UC_User_Scenario.xaml.cs:213` stores `Scenario_Info.Performance = uxPerformance.SelectedIndex`.
So Turbo=0, High=1, Medium=2, Low=3, which is cast to `Shift.ShiftType` (SportMode=0, ComfortMode=1, GreenMode=2, ECO=3)
by `UserScenario.setShift`.

### 4.2 Value → EC byte
`One_Dragon_Center/Base_Module/API_NB_Base_Module.dll/Dragon_Center.Controls.TuneMgr.SetFunction/Shift.cs:403-427`
(`CalculateNewShiftModeValueInEC`, `ChangeToCurrentShiftType`): `v = ((v & 0xC3) | 0xC0) & 0xFC`, then +4 Sport, +0 Comfort,
+1 Green, +2 ECO, +3 User. The result is always:

| UI | ShiftType | EC `0xF2` |
|---|---|---|
| Turbo | SportMode | `0xC4` |
| High | ComfortMode | `0xC0` |
| Medium | GreenMode | `0xC1` |
| Low | ECO | `0xC2` |

Write path on v1: `Shift.cs:449-456` sends `root\WMI:S:MSI_System:System:7:<value>` to pipe `\\.\pipe\simple`
(served by `MSIService.exe`, the Micro Star SCM service), which sets WMI `MSI_System` instance 7. v2 would use
`Set_Data` on EC `0xD2` (`Shift.cs:444`), which doesn't apply here. msictl writes `MSI_System[7]` directly through
WMI. It's the same block and the same `WSxx` ACPI method, without going through MSIService.

**Measured** (`livetest-result.txt` step 3, `e2e-result.txt`): writing `0xC0`, `0xC1`, `0xC2`, `0xC4` read back exactly.
At idle the readings show no difference, as expected for a power-limit selector. Under load the levels are the
EC/BIOS power profiles; their exact PL values are set inside the EC/BIOS and are not visible to Dragon Center either.

### 4.3 Nothing else changes
- CPU overclock: `UserScenario.Check_CPU_OC` (`UserScenario.cs:784-793`) returns early unless the model name contains "GT" and the
  CPU is one of i9-*HK, i7-10700K(F), i9-10900K(F), i9-9900K, i7-9700K. `isSupportXTU` stays false, and `XTU3SERVICE` isn't installed.
- `powercfg` calls exist only in Gaming Mode / Creator Mode helpers and the brightness code (`CDisplaySettings.cs:239`), not in the
  performance-level path.

### 4.4 GPU offsets in Turbo
- Limits for "nvidia geforce gtx 1660 ti": core max 180, memory max 500 (`UserScenario.cs:1047-1057`). The user's
  `High_GPU_Core=180`, `High_GPU_VRAM=500`, `User_GPU_Core=180`, `User_GPU_VRAM=500` are the slider maximums.
- Trigger chain: the backend writes the registry value `...\Component\Base Module\Scenario\OC`; `OmApSvcBroker.exe` watches it
  (`svc_OmApSvcBroker/OmApSvcBroker/MainWindow.xaml.cs:1105-1116`) and calls `OverClock.setOC` (`OverClock.cs:45-47`). For scenario 1
  that reads `High_GPU_*`, for 5 `User_GPU_*`, and for every other scenario it applies 0/0. It runs
  `gpuControl.exe <core> <memory>` (`OverClock.cs:99-100`).
- `gpuControl.exe` (`MSI_NBFoundation_Service/gpuControl.exe/gpuControl/MainWindow.xaml.cs:82-83`) calls `GInf.dll`
  `SetGPUOC(core)` and `SetMemoryOC(memory)`. `GInf.dll` (x64, in `MSI NBFoundation Service`) is an NVAPI wrapper (imports
  `nvapi_QueryInterface`, `NvAPI_GPU_GetPstates20`).
- **Units are MHz. Measured** with `GInf.GetGInfo` (`livetest-result.txt` step 4): offsets 0/0 → max clocks 2100/6001 MHz;
  offsets 180/500 → 2280/6501 MHz. Readback of the offsets matched both times.
- Offsets are volatile (lost on reboot or driver reset). msictl's supervisor reapplies them while Turbo is selected (§9).
- Dragon Center's OC window (`OC_Option_Window`) has two sliders, "Core Clock Offset" and "VRAM Clock Offset", running 0..`GPU_Core_Maximum`
  / 0..`GPU_VRAM_Maximum` in 1 MHz steps. msictl mirrors this: `msictl oc <core> <memory>` and the tray's
  "Turbo GPU offsets…" dialog both validate against 0-180 / 0-500. Changes apply at once in Turbo and are otherwise saved
  for the next Turbo (`state.ini` `turbo_core` / `turbo_memory`). Tested in `e2e-result.txt` (26/26).
- Super Charger is analysed separately in `supercharger\FINDINGS-SuperCharger.md`.

---

## 5. Fan modes

### 5.1 Fan-mode byte, EC `0xF4` = `MSI_System[9]`

| Bit | Meaning | Evidence |
|---|---|---|
| 7 | Advanced (user curve) | `UserScenario.Adv_Fan` v1, `UserScenario.cs:2483-2484`: `result |= 0x80; result &= 0xBF` on `MSI_System` instance 9 |
| 6 | Basic | cleared together with bit 7 by `Auto_Fan` (`UserScenario.cs:2270`: `result &= 0x3F`) |
| 4 | Silent | set when scenario 3 (Silent) is active, cleared when leaving it: `UserScenario.cs:310-314` (init), `:2145-2153` (resume), `SetScenarioMode` (`(num & 0x10)` on `0xFC0008F4`) |
| 0-3 | `0xD` at rest, not touched | measured |

Auto = `0x0D`, Silent = `0x1D`, Advanced = `0x8D` on this machine.

**Measured** (`livetest-result.txt` step 2, Cooler Boost off, idle):
- Auto `0x0D` reference: fans settling from Cooler Boost speed (~6.7k / 6.5k RPM, still falling)
- Silent `0x1D`: CPU fan 6486 → 6000 RPM, GPU fan 6075 → 3779 RPM within 10 s (and still falling)
- Advanced `0x8D` + every curve point 100: both duty readings 100 %, CPU fan ~5925-6000 RPM, GPU fan rising
- Restore to `0x0D` read back exactly

### 5.2 Fan curve, `MSI_CPU` / `MSI_VGA`
- Layout (prior research, confirmed by Dragon Center's constants `FanCPUT1Percentage = 11` ... `FanVGAT6Percentage = 16`,
  `UserScenario.cs:30-64`): `[1]` temperature, `[2]` current duty, `[3]` duty ceiling, `[5..10]` temperature steps, `[11..17]` duty per step.
- Dragon Center's Advanced mode writes `[11..16]` (six points) through WMI (`UserScenario.cs:2445-2473`), then sets `MSI_System[9]` bit 7.
  msictl's Max also writes `[17]` (the top step, stock 85), because prior research proved `[11..17]` = 100 drives duty to 100.
- Stock duty points: CPU `38,43,48,54,60,70,85`, VGA `0,43,48,54,60,70,85`. Dragon Center's own registry `Default_Fan`
  (`38;43;48;54;60;70;0;43;48;54;60;70`) matches `[11..16]`.
- Dragon Center's Auto (`setFan(0)`, `UserScenario.cs:2210-2216`) rewrites the default curve and then runs `Auto_Fan`. msictl does the same.
- **Max** = Advanced with every point at 100 → about 6000 RPM, the normal 100 % ceiling.
- The ceiling byte `[3]` (prior research, raised to 150/255) is **not** what Cooler Boost uses and msictl never writes it.

### 5.3 Cooler Boost, EC `0x98` bit 7
- `UserScenario.IsCoolerBoost` (`UserScenario.cs:104-174`) v1: read `MemRwService.Read_To_Byte_Array("98")[0]`, then
  `num | 0x80` (on) or `num & 0x7F` (off), then `MemRwService.Write("98", ...)`.
- `MemoryIO/MemRwService.cs:14`: base address `"FC0008"`, so EC register `0x98` is physical `0xFC000898`. `Write` reads the 32-bit
  word, keeps the upper three bytes and replaces the low byte (`MemRwService.cs:176-180`). msictl does exactly this.
- No WMI block mirrors `0x98`, which is why the prior brute-force search of the nine blocks never found Cooler Boost.
- **Measured** (`livetest-result.txt` step 1): Dragon Center had it on (`0x82`). Clearing bit 7 (`0x02`) made the fans fall from
  7164 / 8571 RPM toward ~6500 within 10 s. Setting it again restored 7164 / 8571 RPM immediately. So Cooler Boost runs
  about 20-40 % above the 100 %-duty speed.
- Fn+F8 toggles the same bit in hardware. msictl reads it on every `status` and never reverts it.

### 5.4 How msictl maps the four requested modes

| msictl | `MSI_CPU/VGA[11..17]` | `MSI_System[9]` (EC `0xF4`) | EC `0x98` bit 7 | Dragon Center equivalent |
|---|---|---|---|---|
| `auto` | stock curve | bits 7,6,4 clear (`0x0D`) | 0 | Fan = Auto (`setFan(0)`) |
| `max` | all 100 | bit 7 set (`0x8D`) | 0 | Fan = Advanced, every point 100 (`Adv_Fan`) |
| `silent` | stock curve | bit 4 set (`0x1D`) | 0 | Silent scenario's fan bit |
| `boost` | stock curve | `0x0D` | 1 | Fan = Cooler Boost (`setFan(2)`) |

---

## 6. MSI Foundation Service pipe `\\.\pipe\MSI_SERVICE_2`

Server: `MSIAPService.exe` (service "MSI Foundation Service"), `svc_MSIAPService/MSIAPService/NativeNamedPipeServer.cs`.
Client: `NamedPipeClientLib.dll`, `NativeNamedPipeClient.cs:1163-1354`.

- ACL `D:(A;OICI;GRGW;;;AU)(A;OICI;GA;;;BA)` (`NativeNamedPipeServer.cs:428`): any authenticated user may read/write, so **no elevation is needed**.
- Message-mode pipe, one request per connection.
- Frame: `00 FF` + big-endian int32 total length (payload + 6) + ASCII payload.
- Handshake (sent in plain text): `HS:REQ:<ProductName>;<FileVersion>` → `HS:ACK:<server version>;MSI_SERVICE_2`. Any name is
  accepted and remembered (`NativeNamedPipeServer.cs:564-579`). msictl registers as `MSIControl`.
- Every later message: 3DES-ECB, zero padding, key = `MD5("msiservice" + <name> + yyyyMMdd)`
  (`NativeNamedPipeServer/EncrptyDecrypt.cs:11-25`). The server also tries yesterday's date (`NativeNamedPipeServer.cs:320-336`).
- Commands used by msictl:
  - `IO:RMREQ:<physical address decimal>;1` → `IO:RMRST:1;<uint32 decimal>`, or `0;ERR` on failure
  - `IO:WMREQ:<physical address decimal>;1;<uint32 decimal>` → `IO:WMRST:1`, or `0` on failure
- These IO commands only run when `WmiMajorVersion < 2` (`NativeNamedPipeServer.cs:620-634`), which is the case here.
- The server does the access through `KernCoreLib64.dll` → `GetPhysLong` / `SetPhysLong` (`WinIO.cs:327-340`). That is MSI's own signed
  driver `KernCoreLib64.sys`, loaded as service `WINIO` and already running. msictl loads no driver.
- EC RAM is memory-mapped at physical `0xFC000800` (`MemRwService.cs:14`, and `4227860723 = 0xFC0008F3`, `4227860724 = 0xFC0008F4`
  in `UserScenario.cs`).

Other commands the server offers but msictl doesn't use: `RPREQ`/`WPREQ` (I/O ports), `REG:*`, `WMI:*`, `WMI2:*`, `PC:*` (run process).

## 7. Telemetry

- Temperatures: `MSI_CPU[1]`, `MSI_VGA[1]` (°C). Duty: `[2]` (%).
- Fan RPM: `MSI_AP[2]` low, `[3]` high = CPU fan period; `[4]`/`[5]` = GPU fan. RPM = `60000000 / (period × 2 × 62.5)`
  (`UserScenario.GetFanSpeed`, `UserScenario.cs:2576-2605`). **Measured:** 67 → 7164 RPM, 56 → 8571 RPM with Cooler Boost on;
  these move with every fan change in the logs.
- GPU offsets and max clocks: `GInf.GetGInfo` (`GPUInfoStruct`: pstate, core/memory min/max, `GPUOverClocks`, `MemoryOverClocks`, ...).

The ACPI-WMI blocks carry the "expensive" flag. A fresh boot may deny unelevated reads until an elevated query has
enabled collection, so `msictl status` falls back to elevation when it gets "Access denied".

---

## 8. Dragon Center components and what they do here

| Component | Role relevant to fans/perf | Used by msictl? |
|---|---|---|
| `DCv2.exe` (Dragon Center 2.0.156.0) | UI; sends commands to `MSI.CentralServer` | no |
| `MSI_Central_Service` → `MSI.CentralServer.exe` (MSI SDK) | Loads `Base Module\API_NB_Base Module.dll` (`C_DynLoad_SDK.cs:178`), which applies scenarios. **Reapplies its own scenario after every resume** (`PluginClass.cs:298` → `UserScenario.ResumeAutomatic`) and on AC/battery changes when "Intelligent" is on | no, and it conflicts |
| `OmApSvcBroker.exe` (NBFoundation) | Watches the `OC` registry value and runs `gpuControl.exe` | no (msictl calls `GInf.dll` itself) |
| `MSIAPService.exe` = "MSI Foundation Service" | `MSI_SERVICE_2` pipe: EC memory read/write via WINIO | **yes**, Cooler Boost |
| `GInf.dll` in `MSI NBFoundation Service` | NVAPI wrapper | **yes**, GPU offsets |
| `KernCoreLib64.sys` (service `WINIO`) | MSI's physical-memory/port driver used by MSIAPService | indirectly (through the service) |
| `MSIService.exe` = "Micro Star SCM" | Fn hotkeys, `\\.\pipe\simple` WMI proxy | no (msictl uses WMI directly) |
| `wmiacpi.sys` (Windows) | ACPI-WMI blocks | **yes** |
| `MsIo64.sys` (`MSIO`), `EneIo64.sys` (`EneTechIo`) | Mystic Light / SDK hardware access | no |
| `MSI_Companion_Service` (Game Summary) | game telemetry | no |

---

## 9. Safety design of msictl (and how it was tested)

- **Snapshot before every write** (`data\snapshots.log`); on error or Ctrl+C the snapshot is restored. If the snapshot was Silent, the tool
  falls back to Auto instead, because Silent must never run unguarded.
- **Silent guard.** A hidden `msictl supervise` process runs while Silent is selected. It drops to Auto if CPU or GPU reaches 90 °C or
  if temperatures can't be read three times in a row. When it exits for any reason while fans are Silent, it sets Auto.
- **Crash safety.** The supervisor starts a `msictl sentinel <pid>` process that waits on it. If the supervisor dies (even when killed),
  the sentinel sets Auto if fans are still Silent.
- **Turbo.** The supervisor checks the GPU offsets every 10 s and reapplies +180/+500 if a driver reset cleared them, but only while
  EC `0xF2` still says Turbo.
- **Resume.** The supervisor reapplies the saved fan mode and level 5 s after resume, if it is running.
- It never fights changes made elsewhere (Fn+F8, Dragon Center); it only guards what it set.
- Writes are limited by an allow-list to `MSI_CPU`, `MSI_VGA` and `MSI_System`, plus EC `0x98`. No battery or BIOS blocks, no SMBus,
  and no `Set_BIOS`.

`e2e-result.txt` (19/19 passed): all four fan modes, all four levels with matching GPU offsets, supervisor start/stop, the sentinel
restoring Auto 1 s after the supervisor was hard-killed, the guard tripping Silent → Auto (test-only limit of 40 °C,
`MSICTL_SILENT_LIMIT`, which can only lower the limit), and the supervisor reapplying +180/+500 after the offsets were wiped.

Not yet verified: Valorant/Vanguard with the tool active, and a GPU stability run with +180/+500 (§11).

---

## 10. Removal plan (proposal only; nothing has been uninstalled)

**Keep**

| Keep | Why |
|---|---|
| MSI NBFoundation Service (MSIAPService.exe, GInf.dll, KernCoreLib64.sys/WINIO) | Cooler Boost (EC pipe) and GPU offsets (GInf.dll) |
| Micro Star SCM (`C:\Windows\SysWOW64\MSIService.exe`) | Fn hotkeys, including Fn+F8 Cooler Boost |
| Windows `wmiacpi.sys` | in-box; fan curve, fan mode, performance level |

**Can be uninstalled once you confirm**

| Remove | Effect |
|---|---|
| "Dragon Center version 2.0.156.0" (`C:\ProgramData\MSI\Dragon Center UWP\`) | UI gone. msictl replaces fan + performance only. Mystic Light, battery threshold, True Color, etc. go with it. |
| "MSI SDK" (`C:\Program Files (x86)\MSI\One Dragon Center\`: MSI_Central_Service, MSI.CentralServer, MSI_Companion_Service, Mystic Light incl. `EneIo64.sys`) | **Recommended.** Removes the scenario re-apply after resume that overrides msictl, and one known-vulnerable driver (`EneIo64.sys`). |
| "Super-Charger" | Not part of this work; unrelated to fans/perf. Remove only if you don't use USB fast charging. |

Open items before removal:
1. Battery charge mode (Dragon Center's Battery page): `API_NB_Battery.dll` (`C_Battery.cs`) only writes a registry value
   (`...\Component\Battery...`) through the Foundation pipe. Which service turns that into a hardware write was **not traced**, and it is
   untested whether the last setting persists after removal. If you rely on a charge limit, check it after removal.
2. `MsIo64.sys` (`MSIO`, in `C:\Windows\System32\drivers`): the owning installer wasn't confirmed, and msictl doesn't need it. Check whether
   it remains after the MSI SDK uninstall before deciding anything about it.
3. The NBFoundation uninstaller/updater (`OneDC_Updater.exe`) may try to reinstall Dragon Center. Watch for that after the first reboot.
4. After uninstalling, run `msictl status`, then `msictl fan boost` and `msictl fan auto`, to confirm the EC pipe and GInf.dll still work.

---

## 11. Remaining verification

- Launch Valorant with `msictl perf turbo` active (supervisor running) and confirm that Vanguard doesn't block it.
- GPU stability with +180/+500: a few minutes of GPU load while `stability-check.ps1` watches for driver resets
  (nvlddmkm / Display 4101 events), offset loss and clocks.
