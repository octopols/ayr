# Changelog

## 0.1.0 (unreleased)

First version.

- Fan modes Auto, Max, Silent and Boost (Cooler Boost), with Auto as the failsafe.
- Performance levels Turbo, High, Medium and Low; Turbo applies GPU core and memory offsets.
- Tray app and command line (`ayrctl`) with the same controls.
- Silent guard: back to Auto at 90 °C, and a watchdog process that forces Auto if the guard dies.
- Every change is snapshotted first and rolled back on error or Ctrl+C.
- Apple fast charge through MSI Super Charger's own helper, one request per plug-in.
- Optional logon task that restores your settings and opens the tray, replacing MSI's background services.
- Self-contained: MSI's six signed files ship in `vendor\` and are verified before use, so no MSI software is needed.
  Ayr registers and locks down MSI's drivers itself and sets up MSI's firmware interface on laptops that lack it.
- Works without an NVIDIA GPU (no GPU offsets) or without EC access (no Cooler Boost); everything else keeps working.
- Warns while MSI Center runs, and never sends Apple requests while Super Charger's own service runs.
- `ayrctl uninstall` sets the fans to Auto and removes everything Ayr registered.
