# Supported models

Ayr writes to the embedded controller, so it only switches modes on models whose register layout has been verified.
This page lists those models, explains what differs between models, and shows how to check yours safely and help add it.

## Verified

| Model | Board | Firmware path | Fans & performance | GPU offsets | Apple fast charge |
|---|---|---|---|---|---|
| GF65 Thin 10SDR | MS-16W1 | v1 | ✅ | ✅ GTX 1660 Ti, 0-180 / 0-500 MHz | ⚠️ request sent, charging speed not yet measured |

## Probably compatible, not yet verified

Other MSI laptops on the **v1 firmware path** (10th-gen Intel and earlier, see [how-it-works.md](how-it-works.md)) use
the same WMI blocks and very likely the same registers. What can still differ:

| What | Why it matters |
|---|---|
| The stock fan curve | Auto writes it back, so a wrong curve gives wrong fan speeds in Auto until the right one is written again (MSI Center's Auto does that) |
| The EC RAM address | Cooler Boost is written there |
| The performance-mode values | Wrong values could select a different power profile |
| The GPU offset limits | They depend on the GPU model |

Until a model is verified, treat Ayr as **read-only** on it: `ayrctl status` and `ayrctl probe` only read.

## Not supported yet

- **v2 firmware** (11th-gen Intel / Tiger Lake and newer). It uses different WMI methods that Ayr doesn't implement yet.
- **AMD-based models:** which path they use hasn't been checked. A probe report would answer it.
- **AMD Radeon GPUs.** GPU offsets go through NVIDIA's NVAPI only. Fans and performance don't depend on the GPU.

## Check your laptop (read-only)

1. Keep MSI Center or Dragon Center installed for now, and note the performance level and fan mode it shows.
2. Download Ayr and run, from a terminal in its folder:

   ```powershell
   .\ayrctl.exe probe
   .\ayrctl.exe status
   ```

   Neither command writes anything.
3. Compare with [how-it-works.md](how-it-works.md):
   - `MSI_System` should have about 21 entries, with `[7]` matching one of `196` (Turbo), `192` (High), `193` (Medium)
     or `194` (Low) for the level MSI Center shows.
   - `MSI_CPU` and `MSI_VGA` entries 11-17 should be a rising fan curve.
   - `EC 0x98` should change bit 7 when you press the Cooler Boost key.
   - `EC 0xF2` and `EC 0xF4` should equal `MSI_System[7]` and `MSI_System[9]`.

## Help add your model

Open a **Model report** issue with your model, BIOS version and the two outputs above. If everything matches, the model
can be added to the verified list after someone with that laptop runs `scripts/e2e-test.ps1`.
