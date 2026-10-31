# Changelog

## 0.1.0 (unreleased)

First version.

- Fan modes Auto, Max, Silent and Boost (Cooler Boost), with Auto as the failsafe.
- Performance levels Turbo, High, Medium and Low; Turbo applies GPU core and memory offsets.
- Tray app and command line (`ayrctl`) with the same controls.
- Silent guard: back to Auto at 90 °C, and a watchdog process that forces Auto if the guard dies.
- Every change is snapshotted first and rolled back on error or Ctrl+C.
- Apple fast charge through MSI Super Charger's own helper, one request per plug-in.
- Optional logon task that restores your settings, replacing MSI's background services.
