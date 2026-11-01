# Supported models

Ayr writes to the laptop's embedded controller, so it only changes modes on models that have been checked. This page
lists them and shows how to check your own laptop safely.

## Contents

1. [Tested devices](#tested-devices)
2. [Probably compatible](#probably-compatible)
3. [Not supported yet](#not-supported-yet)
4. [Check your laptop](#check-your-laptop)
5. [Get your model added](#get-your-model-added)
6. [Notes for AI](#notes-for-ai)

## Tested devices

| Device | Board | Fan modes | Cooler Boost | Performance | GPU overclock | Charge limit | Keyboard light | Apple fast charge | Start with Windows |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| MSI GF65 Thin 10SDR | MS-16W1 | <img src="images/status/works.svg" width="16" alt="Works"> | <img src="images/status/works.svg" width="16" alt="Works"> | <img src="images/status/works.svg" width="16" alt="Works"> | <img src="images/status/works.svg" width="16" alt="Works"> | <img src="images/status/works.svg" width="16" alt="Works"> | <img src="images/status/partial.svg" width="16" alt="Partly tested"> ² | <img src="images/status/partial.svg" width="16" alt="Partly tested"> ¹ | <img src="images/status/works.svg" width="16" alt="Works"> |

<img src="images/status/works.svg" width="12" alt=""> Works &nbsp;
<img src="images/status/partial.svg" width="12" alt=""> Partly tested &nbsp;
<img src="images/status/not-on-device.svg" width="12" alt=""> Device doesn't have it &nbsp;
<img src="images/status/unsupported.svg" width="12" alt=""> Not supported by Ayr yet

1. The request is sent and the PC stays stable, but nobody has measured yet whether charging gets faster.
2. Reading the level works; setting it from Ayr hasn't been tried on this laptop yet.

**MSI desktop motherboards** are untested. Fans, Cooler Boost, performance and the GPU overclock are not supported on
them, because desktop fans are run by a different chip. Apple fast charge doesn't depend on the board and may work.

## Probably compatible

Other MSI laptops with 10th-gen Intel or older use the same firmware interface (the "v1 path" in
[how-it-works.md](how-it-works.md)) and most likely the same registers. What can still differ:

| What | Why it matters |
|---|---|
| Stock fan curve | Auto writes it back, so a wrong curve gives wrong fan speeds in Auto |
| EC RAM address | Cooler Boost is written there |
| Performance-mode values | A wrong value could pick a different power profile |
| GPU offset limits | They depend on the GPU |

Until a model is checked, use only the read-only commands on it.

## Not supported yet

- **11th-gen Intel and newer:** different firmware methods that Ayr doesn't implement yet.
- **AMD laptops:** not checked. A probe report would tell.
- **AMD Radeon GPUs:** no GPU overclock (it uses NVIDIA's API). Fans and performance still work.

## Check your laptop

Both commands only read:

```powershell
.\ayrctl.exe probe
.\ayrctl.exe status
```

Compare the output with what MSI Center showed:

- `MSI_System[7]` is `196` Turbo, `192` High, `193` Medium or `194` Low, matching MSI Center's level.
- `MSI_CPU` and `MSI_VGA` entries 11-17 form a rising fan curve.
- `EC 0x98` bit 7 changes when you press the Cooler Boost key.
- `EC 0xF2` and `EC 0xF4` equal `MSI_System[7]` and `MSI_System[9]`.

## Get your model added

Open a **Model report** issue with your model, BIOS version and both outputs. If they match, the model is added once
someone with that laptop runs `scripts/e2e-test.ps1`.

## Notes for AI

**Decisions**
- Start with Windows marked as working after a real reboot restored the settings at sign-in on the GF65 (2026-11-01).
- Status icons are SVGs in `docs/images/status/`, not emoji, so they look the same everywhere. Use the same four in
  both this table and the README's.
