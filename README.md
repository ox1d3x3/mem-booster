# Mem-Booster by Ox1d3x3 v0.5.19

Native Windows WPF app for quickly preparing Windows for gaming by closing selected background apps and restoring them later where possible.

## Highlights

- Fast native .NET 8 WPF interface
- Live RAM usage and process list
- Checkbox/app-name selection plus selected-apps panel
- Local profiles and shareable XML profiles
- Safe Select, Extreme Select and Aggressive Select
- Smart close first, then force-kill process trees
- Restore Last to reopen apps captured during the previous boost
- Device Optimise / Revert Device Optimise for reversible Windows 11 gaming-session settings
- Dark/light theme toggle
- GitHub update check: https://github.com/ox1d3x3/mem-booster
- Detailed logs and performance timings for troubleshooting

## Selection modes

- Safe Select: conservative list of common background apps.
- Extreme Select: larger known non-gaming cleanup list.
- Aggressive Select: stronger gaming-session cleanup for browsers, Office/M365, widgets, PC Manager, Windows feed/search/index helpers, cloud sync tools, dev tools, media apps, PowerToys, Command Palette helpers and download managers.

Aggressive Select still excludes core Windows processes, game launchers, anti-cheat, GPU driver/control processes, Discord/voice/streaming tools, VPN/firewall/security tools, RGB/fan/driver-tuning tools, MSI Afterburner and RivaTuner. Review the selected-apps panel before pressing BOOST NOW.

## Logs

Mem-Booster writes detailed logs here:

```text
%APPDATA%\Mem-Booster\logs
```

Useful files:

```text
mem-booster.log       Main activity log
performance.log       Operation timings
boost.log             Boost/restore results
device-optimise.log   Device Optimise/Revert details
snapshots\*.csv       Before/after process snapshots
startup.log           Startup crash log
```

Use the **Logs** button in the app or run:

```bat
open-logs.bat
```

## Restore Last

Before boosting, Mem-Booster captures restorable executable paths for selected running apps. After boosting, Restore Last reopens those apps where possible and skips apps already running. It is best-effort and will not restore unsaved documents, exact window positions, browser tabs, every UWP app state, or protected service state.

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

## Diagnostics

Use the **Diagnostics** button inside the app, or run:

```bat
collect-diagnostics.bat
```

It creates a ZIP on your Desktop containing logs, boost/restore history, process snapshots, restore session, local profile, settings, and current process list. Share that ZIP when reporting issues.

## v0.5.19 reliability notes

- Aggressive Select no longer auto-selects unknown process names. Unknown apps stay manually selectable, but Aggressive mode is now deterministic and safer.
- Existing local/XML profiles are sanitised on load so old unsafe entries such as Windows service processes, VM service helpers, OOBE broker and temporary helper processes are dropped automatically.
- Save Local and Export XML skip temporary/unsafe profile entries so old aggressive selections do not keep coming back.
- Monitoring/LCD tools such as HWiNFO, TrafficMonitor, TRCC, USBLCD and USBLCDNEW are no longer selected by Aggressive mode. They remain manual review items.
- Restore Last is more conservative and no longer attempts to relaunch service/helper processes such as SearchIndexer, SearchProtocolHost, sqlwriter, VMware services or Windows OOBE broker.
- Successful Boost/Restore now reports in the status bar instead of forcing an extra completion pop-up, which keeps the workflow smoother and makes operation timing logs more accurate.
- Logs are rotated at 5 MB so long test sessions do not create oversized log files.

## Device Optimise

Device Optimise is Windows 11 focused and requires administrator mode for the full workflow. The button opens a selectable optimisation list with a 15-second review timer. Recommended safe options are selected by default, while advanced/restart-required options are clearly marked.

Recommended reversible options include Ultimate Performance power plan, Windows Game Mode, Xbox/Game DVR capture/background recording disabled, transparency/animation effects reduced, Widgets taskbar button hidden, and Windows Search indexing paused if it was running.

Optional advanced options include enhanced pointer precision off, multimedia scheduler Games profile values, and hardware-accelerated GPU scheduling. These are still captured and reversible, but they are not selected by default because they can change input feel, audio/network scheduling behaviour, or require a restart.

Revert Device Optimise restores the captured power plan, registry values and Windows Search state. Mem-Booster intentionally does not disable antivirus/security, Memory Integrity/VBS, HPET, GPU drivers, network adapters, VPN/firewall tools, game launchers, anti-cheat, Discord, MSI Afterburner or RivaTuner.

## v0.5.19 notes

- Device Optimise now opens a selectable optimisation list instead of applying a fixed set.
- Recommended items are selected by default; advanced/restart-required items are opt-in.
- Revert Device Optimise lists the captured options before reverting.
- Device Optimise uses reversible settings only and stores the baseline in `%APPDATA%\Mem-Booster\device-optimise-state.xml`.
