# Mem-Booster by Ox1d3x3 v0.5.26

Native Windows 11 WPF app for preparing a gaming session by closing selected non-gaming background apps, applying optional reversible Windows 11 gaming-session settings, and keeping clear logs for troubleshooting.

## Highlights

- Fast native .NET 8 WPF interface
- Live RAM usage and process list
- Checkbox/app-name selection plus selected-apps panel
- Local profiles and shareable XML profiles
- Safe Select, Extreme Select and Aggressive Select
- Smart close first, then force-kill process trees
- Device Optimise / Revert Device Optimise for reversible Windows 11 gaming-session settings
- Restart guidance after Boost instead of unreliable app relaunch attempts
- Modern dark/light theme toggle
- GitHub update check: https://github.com/ox1d3x3/mem-booster
- Detailed logs and performance timings for troubleshooting

## Selection modes

- Safe Select: conservative list of common background apps.
- Extreme Select: larger known non-gaming cleanup list.
- Aggressive Select: stronger gaming-session cleanup for browsers, Office/M365, widgets, PC Manager, Windows feed/search/index helpers, cloud sync tools, dev tools, media apps, PowerToys, Command Palette helpers and download managers.

Aggressive Select still excludes core Windows processes, game launchers, anti-cheat, GPU driver/control processes, Discord/voice/streaming tools, VPN/firewall/security tools, RGB/fan/driver-tuning tools, MSI Afterburner and RivaTuner. Review the selected-apps panel before pressing BOOST NOW.

## Boost behaviour

Mem-Booster closes selected app process trees. It does not try to reopen apps automatically because packaged apps, helper processes, UWP apps, browsers and background services do not restore reliably enough.

After a heavy boost, restart Windows to return to the normal background-app state. If you used Device Optimise, use Revert Device Optimise first, then restart if needed.

## Device Optimise

Device Optimise is Windows 11 focused and requires administrator mode for the full workflow. The button opens a selectable optimisation list with a 15-second review timer. Recommended safe options are selected by default, while advanced/restart-required options are clearly marked.

Recommended reversible options include Ultimate Performance power plan, Windows Game Mode, Xbox/Game DVR capture/background recording disabled, transparency/animation effects reduced, Widgets taskbar button hidden, and Windows Search indexing paused if it was running.

Optional advanced options include enhanced pointer precision off, multimedia scheduler Games profile values, and hardware-accelerated GPU scheduling. These are captured and reversible where supported, but they are not selected by default because they can change input feel, audio/network scheduling behaviour, or require a restart.

Revert Device Optimise restores the captured power plan, registry values and Windows Search state. Mem-Booster intentionally does not disable antivirus/security, Memory Integrity/VBS, HPET, GPU drivers, network adapters, VPN/firewall tools, game launchers, anti-cheat, Discord, MSI Afterburner or RivaTuner.

## Logs

Mem-Booster writes detailed logs here:

```text
%APPDATA%\Mem-Booster\logs
```

Useful files:

```text
mem-booster.log       Main activity log
performance.log       Operation timings
boost.log             Boost results
device-optimise.log   Device Optimise/Revert details
snapshots\*.csv       Before/after process snapshots
startup.log           Startup crash log
```

Use the **Logs** button in the app or run:

```bat
open-logs.bat
```

## Diagnostics

Use the **Diagnostics** button inside the app, or run:

```bat
collect-diagnostics.bat
```

It creates a ZIP on your Desktop containing logs, process snapshots, local profile, settings, and current process list. Share that ZIP when reporting issues.

## v0.5.26 notes

- Added optional **Fast boost** mode. This skips graceful close and force-closes selected apps faster. It is off by default.
- Boost now pauses auto-refresh while the warning dialog is open, reducing UI churn during confirmation.
- Auto-refresh performance logging is now quieter. Normal fast refreshes are not written to the performance log; only slow auto-refreshes or failures are logged.
- Manual refresh, initial load, boost, device optimise, update checks and diagnostics still keep detailed timing logs.
- Selection behaviour from v0.5.25 is retained: click app name or checkbox to select/remove.

## Build

Open `MemBooster.sln` in Visual Studio 2022 with the .NET desktop development workload installed.

Or publish a self-contained x64 build:

```bat
clean-publish-win-x64.bat
```

Output:

```text
src\MemBooster\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\Mem-Booster.exe
```
