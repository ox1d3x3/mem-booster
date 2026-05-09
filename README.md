# Mem-Booster/Background Task killer by Ox1d3x3

Mem-Booster is a native Windows 11 utility designed to prepare your PC for a cleaner gaming session. It helps close selected non-gaming background apps, reduce unnecessary background activity, and optionally apply reversible Windows 11 gaming-session optimisations.

**The goal is simple**: free up system resources before gaming without using unsafe “debloat” methods that can break Windows, security tools, drivers, launchers, overlays, or anti-cheat systems.

---

## Features

- Native Windows 11 app built with .NET 8 WPF
- Local profiles and shareable XML profiles
- Safe Select, Extreme Select, and Aggressive Select modes
- Optional Fast Boost mode
- Reversible Windows 11 Device Optimise options

---

## Basic usage

1. Open **Mem-Booster**.
2. Review the running apps list.
3. Select apps manually or use **Safe Select**, **Extreme Select**, or **Aggressive Select**.
4. Review the **Selected apps** panel.
5. Optional: click **Device Optimise** and choose the Windows 11 settings you want to apply.
6. Click **BOOST NOW**.
7. After gaming, use **Revert Device Optimise** if you applied Device Optimise.
8. Restart Windows if you want all closed background apps and services to return normally.

---

## Profiles

Mem-Booster supports local and shareable XML profiles.

- **Save Local** saves your selected apps on your PC.
- **Export XML** creates a shareable profile.
- **Load XML** imports a profile from another user or system.

If a profile contains apps that are not installed or not currently running, Mem-Booster skips them automatically.


---

## How it works

Mem-Booster scans running user-level apps and groups them by executable name. You can manually select apps, load a saved profile, or use one of the built-in selection modes.

When you press **BOOST NOW**, Mem-Booster closes the selected apps and related process trees. It skips missing apps automatically and avoids protected Windows, driver, security, gaming, overlay, VPN, firewall, and anti-cheat related processes.

Mem-Booster does not attempt to restore closed apps automatically. After a heavy boost, restart Windows to return to a normal background-app state.

---

## Selection modes

### Safe Select

Selects common background apps that are usually safe to close before gaming.

### Extreme Select

Selects a larger known list of non-gaming apps, including browsers, Office/Microsoft 365 helpers, widgets, cloud sync apps, dev tools, download managers, and similar background software.

### Aggressive Select

Selects a stronger gaming-session cleanup list. This is intended for users who want a more focused gaming session and are comfortable closing more non-gaming background apps.

Always review the **Selected apps** panel before pressing **BOOST NOW**.

---

## Device Optimise

Device Optimise provides selectable Windows 11 gaming-session settings. Each option can be reviewed before applying, and supported options can be reverted with **Revert Device Optimise**.

Examples of supported optimisation areas:

- Ultimate Performance power plan
- Windows Game Mode
- Xbox/Game DVR capture/background recording
- Visual effects such as transparency and animations
- Widgets taskbar button
- Windows Search indexing pause
- Optional advanced settings such as enhanced pointer precision, multimedia scheduler profile, and hardware-accelerated GPU scheduling

Mem-Booster intentionally does not disable or modify antivirus, Defender, Memory Integrity/VBS, HPET, GPU drivers, game launchers, anti-cheat, Discord, MSI Afterburner, RivaTuner, VPNs, firewalls, RGB tools, or fan-control tools.

---

## Requirements

### To run the published app

If you use the general release build, no separate .NET runtime required.

For Portable install:
- **.NET 8 Desktop Runtime**
Download: https://dotnet.microsoft.com/en-us/download/dotnet/8.0


### BYO - Build Your Own

Install:
- Visual Studio 2022
- .NET 8 SDK
- .NET desktop development workload

---

## Logs and diagnostics

Logs are stored here:

```text
%APPDATA%\Mem-Booster\logs
```

Useful log files:

```text
mem-booster.log       Main activity log
performance.log       Operation timings
boost.log             Boost results
device-optimise.log   Device Optimise and revert details
startup.log           Startup crash log
snapshots\*.csv       Before/after process snapshots
```

Use the **Logs** button to open the logs folder.

Use the **Diagnostics** button, or run:

```bat
collect-diagnostics.bat
```

This creates a ZIP file on your Desktop containing logs, process snapshots, local profile, settings, and current process details.

---

## Build from source

Open:

```text
MemBooster.sln
```

To publish a self-contained x64 build, run:

```bat
clean-publish-win-x64.bat
```

The published executable will be created under:

```text
src\MemBooster\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\Mem-Booster.exe
```

---

## Project

Created by **Ox1d3x3**

--- 
## Disclaimer

Mem-Booster is provided **as is**, with **no warranty** of any kind.

Use it at your own risk. The app is designed to avoid unsafe Windows modifications, but closing apps and changing system settings can still affect running programs, unsaved work, background sync, overlays, notifications, or other active tasks.

Always save your work before using **BOOST NOW** or **Device Optimise**. Restart Windows if you want to return to a normal background-app state.


GitHub: `https://github.com/ox1d3x3/mem-booster`
