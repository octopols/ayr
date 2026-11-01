# Changelog

## 0.4.0 (2026-11-01)

- **Custom fan curve:** a fifth fan mode. Drag the points of the CPU and GPU curves, labelled with your firmware's
  own temperatures; changes apply when you let go. The hottest points can't go below Auto's speed, and no point can be
  lower than the one before it.
- **Charge limit:** Full, or a custom limit from 50 to 95% to make a mostly plugged-in battery last longer. Ayr
  re-applies it at sign-in and after sleep.
- **Keyboard light:** Off, Low, Medium or High, in step with the Fn keys.
- Charge limit and keyboard light only appear on laptops whose EC firmware is known to keep them where Ayr writes them
  (GF63 and GF65 Thin, 9th and 10th generation, for now).
- **Fn+F7** opens Ayr's quick menu. It opened Dragon Center before, and did nothing once MSI's apps were removed.
- The tray menu shows all five fan modes in one row.
- `ayrctl fan custom`, `ayrctl curve`, `ayrctl charge`, `ayrctl light`.
- Game mode also finds GOG and Ubisoft Connect games.
- A launcher's damaged record no longer hides the other games it lists.
- `ayrctl game add` and `remove` take one path; quote it if it has spaces.

## 0.3.0 (2026-11-01)

- **Game mode:** while one of your games runs, Ayr switches to Turbo with your GPU overclock and Cooler Boost, and puts
  your everyday settings back when the last game closes. It also restores them after sleep, and if Ayr exits mid-game.
- Ayr finds your games: from Riot, Steam and Epic, from Windows' own list of games it has seen run, and from game
  folders such as `D:\Games`. **Scan again** looks again at any time, and **Add another game…** adds anything else.
- A game is watched by its whole folder, so it counts no matter which of its programs is running.
- Detection uses Windows' process start and stop events, so nothing is polled.
- `ayrctl game` shows and changes Game mode and your games.

## 0.2.0 (2026-11-01)

- Apple fast charge shows each device's state: a green "Fast charging" badge once the request has been sent for the
  current connection, which resets when the device is unplugged.
- The toggle is now **Send automatically**: every device gets the request when plugged in, and switching it on also
  sends to devices already connected. With it off, each device has its own **Send** button, greyed out with an
  explanation once sent, while plugging in, or during the 5-minute gap between requests.
- `ayrctl apple boost` is now `ayrctl apple send`, and refuses to resend to a device that already has the request.
- Updates: **Check for updates** in Settings, and **Automatic updates** (on by default) that checks GitHub once a day,
  verifies the download against its published SHA-256, installs it and restarts the tray with your settings.

## 0.1.0 (2026-11-01)

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
