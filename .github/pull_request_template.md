## What and why

<!-- What does this change, and what problem does it solve? -->

## Checks

- [ ] `scripts/build.ps1` builds with no warnings
- [ ] If this touches the hardware path: tested on a real laptop (model, BIOS) and `scripts/e2e-test.ps1` passes
- [ ] No new driver is loaded, and nothing writes to BIOS, battery or SMBus registers
- [ ] The tray's idle footprint is unchanged (no new timers or polling while no window is open)
