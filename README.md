<div align="center">

<img src="src/MemBooster/Assets/mem-booster-logo.png" width="120" alt="Mem-Booster logo" />

# Mem-Booster

**A native Windows 11 utility that frees up system resources before a gaming session — safely.**

[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET%208-WinUI%203-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/version-0.6.21-22C55E)](https://github.com/ox1d3x3/mem-booster/releases)
[![Build](https://img.shields.io/badge/build-self--contained%20x64-0F6B3E)](#build-from-source)

*Created by **Ox1d3x3***

</div>

---

## Overview

Mem-Booster prepares your PC for a cleaner gaming session by closing selected non-gaming background apps, reducing unnecessary background activity, and optionally applying **reversible** Windows 11 gaming-session optimisations.

The goal is simple: **free up system resources before gaming without unsafe "debloat" tricks** that can break Windows, security tools, drivers, launchers, overlays, or anti-cheat systems. Mem-Booster deliberately stays away from anything that could destabilise your system.

---

## Screenshots

### Main view — Dark theme
The dashboard shows live system memory, running app count, your current selection, and profile status. Apps are grouped by executable, sorted by memory use, and labelled with a safety status.

![Mem-Booster main view in dark theme](docs/screenshots/01-dark-mode.png)

### Main view — Light theme
A single toggle in the header switches between a polished dark and light theme, animated smoothly.

![Mem-Booster main view in light theme](docs/screenshots/02-light-mode.png)

### Load an XML profile
Profiles are portable XML files. Load a shared profile, or one of the bundled examples, straight from disk.

![Loading an XML profile in Mem-Booster](docs/screenshots/03-load-xml-profile.png)

---

## Features

- **Native Windows 11 app** built with .NET 8 and **WinUI 3** (Windows App SDK).
- **Live system snapshot** — memory usage, running app count, and per-app memory grouped by executable.
- **Three one-click selection modes** — Safe, Extreme, and Aggressive.
- **Safety-aware** — protected system, driver, security, launcher, overlay, VPN, and anti-cheat processes are never auto-selected.
- **Local and shareable profiles** — save your selection locally or export/import portable XML profiles.
- **Reversible Device Optimise** — opt-in Windows 11 gaming-session settings that can be rolled back.
- **Stop Services** — pause non-essential Windows services (telemetry, updaters, error reporting) from a curated safe list, and bring them all back with one click.
- **Publisher column** — see who makes each app, and search by publisher.
- **Reclaimable-memory estimate** — see roughly how much RAM your selection will free before you boost.
- **Freed-RAM readout** — after each boost the memory card shows how much free RAM you actually gained, updated again once Windows settles.
- **Light footprint** — one native call per scan (no per-process objects), animations run on the GPU compositor and pause in the background, and the app hands its own idle memory back to Windows a few seconds after you switch to your game.
- **Optional Fast Boost** and **Smart close first** modes.
- **Dark and light themes** with smooth animated transitions.
- **Detailed logging and one-click diagnostics** for troubleshooting.

---

## Requirements

**Latest build:** https://github.com/ox1d3x3/mem-booster/releases

| Scenario | What you need |
| --- | --- |
| Release build (self-contained x64) | Windows 11 x64. Nothing else to install — the .NET 8 and Windows App SDK runtimes ship with the app. |
| Building from source | Visual Studio 2022, .NET 8 SDK, and the **.NET Desktop Development** workload with the Windows App SDK C# templates |

---

## Basic usage

1. Open **Mem-Booster**.
2. Review the running apps list.
3. Select apps manually, or use **Safe Select**, **Extreme Select**, or **Aggressive Select**.
4. Review the **Selected apps** panel on the right.
5. *(Optional)* Click **Device Optimise** and choose the Windows 11 settings you want to apply.
6. *(Optional)* Click **Stop Services** and choose which background services to pause.
7. Click **BOOST NOW**.
8. After gaming, use **Revert Device Optimise** and **Restore Services** if you used them.
9. Restart Windows when you want all closed background apps and services to return to normal.

> **Tip:** Apps are scanned once on launch. Use **Refresh Running Apps** to re-scan at any time.

---

## Selection modes

| Mode | What it selects |
| --- | --- |
| **Safe Select** | Common background apps that are usually safe to close before gaming. |
| **Extreme Select** | A larger known list — browsers, Office/Microsoft 365 helpers, widgets, cloud sync apps, dev tools, download managers, and similar background software. |
| **Aggressive Select** | A stronger gaming-session cleanup list for users who want a more focused session and are comfortable closing more non-gaming background apps. |

Always review the **Selected apps** panel before pressing **BOOST NOW**.

---

## Profiles

Mem-Booster supports both local and shareable XML profiles.

| Action | What it does |
| --- | --- |
| **Save Current Profile** | Saves your current selection to this PC. |
| **Export XML** | Creates a portable, shareable `.xml` profile. |
| **Load XML** | Imports a profile and replaces your current selection. |

If a profile contains apps that are not installed or not currently running, Mem-Booster skips them automatically. Hard-protected system processes are never imported.

---

## How it works

Mem-Booster scans running user-level apps and groups them by executable name. You can select apps manually, load a saved profile, or use one of the built-in selection modes.

When you press **BOOST NOW**, Mem-Booster closes the selected apps and their related process trees. It skips missing apps automatically and avoids protected Windows, driver, security, gaming, overlay, VPN, firewall, and anti-cheat processes.

Selected apps that run as **Windows services** (for example a VPN or updater service) are stopped through Windows' Service Control Manager instead of being killed. A killed service is restarted by Windows within seconds; a stopped one stays stopped until you click **Restore Services** or restart. This needs administrator mode, and the boost warning names every service it will stop.

Services are stopped at the same time as other apps are closed, so they add little to boost time. Services with running dependents are never stopped; Windows itself enforces this.

Process trees are matched safely: Windows remembers a process's parent ID even after the parent exits, and IDs get reused, so a process only counts as a child if it started *after* its parent. An unrelated app can't be swept into a target's tree by a recycled ID.

A few seconds after each boost, Mem-Booster re-scans. Apps that came straight back (Windows shell hosts, apps with watchdogs) are marked **Respawns** in the Status column. Later boosts leave them running instead of closing them for nothing, and they're left out of the reclaimable-memory estimate. A tag expires 7 days after it was last confirmed; the next boost then tries once more and re-tags the app if it comes back.

The full boost warning appears on the first boost of a session; repeat boosts run immediately, unless a new service would be stopped. Extra clicks within 1.5 seconds of a boost finishing are ignored.

On launch, Mem-Booster forgets any stopped service that Windows has already restarted (for example after a reboot), so **Restore Services** is only enabled when something is actually stopped.

The memory card then shows **Last boost freed ≈ X**: the difference in free physical RAM before and after the boost. Other programs keep allocating in the meantime, so treat it as an honest estimate rather than an exact figure.

Mem-Booster does **not** attempt to restore closed apps automatically. After a heavy boost, restart Windows to return to a normal background-app state.

---

## Device Optimise

Device Optimise offers selectable Windows 11 gaming-session settings. Each option can be reviewed before applying, and supported options can be rolled back with **Revert Device Optimise**.

Supported optimisation areas include:

- Ultimate Performance power plan
- Windows Game Mode
- Xbox / Game DVR capture and background recording
- Visual effects such as transparency and animations
- Widgets taskbar button
- Windows Search indexing pause
- Advanced options such as enhanced pointer precision, multimedia scheduler profile, and hardware-accelerated GPU scheduling

Mem-Booster intentionally does **not** disable or modify antivirus, Defender, Memory Integrity / VBS, HPET, GPU drivers, game launchers, anti-cheat, Discord, MSI Afterburner, RivaTuner, VPNs, firewalls, RGB tools, or fan-control tools.

---

## Stop Services

**Stop Services** (requires administrator mode) lists the services from Mem-Booster's safe list that are running right now, and lets you choose which to pause for your session.

| Group | Examples | Default |
| --- | --- | --- |
| **Recommended** | Telemetry (DiagTrack), Windows Error Reporting, Program Compatibility Assistant, Downloaded Maps, Fax, Geolocation, and browser/Adobe background updaters | Ticked |
| **Optional** | SysMain, Print Spooler, BITS, Windows Update, Delivery Optimization | Unticked |

How it stays safe:

- **Allowlist only.** Mem-Booster can only ever stop services on its built-in list — nothing else, whatever is requested.
- **Stop, never reconfigure.** Startup types are never changed, so a restart always returns every service to normal.
- **No cascades.** A service with any running dependent is skipped rather than taking other services down with it.
- **Remembered.** Every service Mem-Booster stops is recorded, so **Restore Services** works even after you close and reopen the app.

Some services (Windows Update, BITS, Delivery Optimization) can be restarted by Windows on demand — that's expected and harmless.

---

## Logs and diagnostics

Logs are stored at:

```text
%APPDATA%\Mem-Booster\logs
```

| File | Contents |
| --- | --- |
| `mem-booster.log` | Main activity log |
| `performance.log` | Operation timings |
| `boost.log` | Boost results |
| `device-optimise.log` | Device Optimise and revert details |
| `services.log` | Stop Services, Restore Services and boost app-service details |
| `startup.log` | Startup crash log |
| `snapshots\*.csv` | Before / after process snapshots (newest 40 kept) |

- Use the **Logs** button to open the logs folder.
- Use the **Diagnostics** button to collect a support bundle (logs, process snapshots, local profile, settings, and current process details) into a single ZIP on your Desktop.

---

## Build from source

Open the solution in Visual Studio 2022:

```text
MemBooster.WinUI.sln
```

To publish a self-contained x64 build from the command line:

```bat
clean-publish-win-x64.bat
```

The published executable is created under:

```text
src\MemBooster\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\Mem-Booster.exe
```

Other helper scripts in the repository root:

| Script | Purpose |
| --- | --- |
| `run-dev.bat` | Run a local development build |
| `verify-build.bat` | Quick build verification |

---

## Project structure

```text
mem-booster/
├─ MemBooster.WinUI.sln          Visual Studio solution
├─ docs/screenshots/             README screenshots
├─ src/MemBooster/
│  ├─ MainWindow.xaml(.cs)        Main UI and interaction logic
│  ├─ App.xaml(.cs)               Application entry point
│  ├─ AppInfo.cs                  Version and links (one place to bump)
│  ├─ Assets/                     Logo and icons
│  ├─ Models/                     ProcessGroup, SelectedAppItem
│  ├─ Converters/                 XAML value converters
│  ├─ Profiles/                   Bundled example XML profiles
│  └─ Services/                   Core logic:
│     ├─ ProcessService           Scanning, grouping, process-tree termination
│     ├─ NativeProcessSnapshot    One-call native process list (PID, parent, name, memory)
│     ├─ SelfTrimService          Returns Mem-Booster's own idle memory while in the background
│     ├─ MemoryService            System memory readout
│     ├─ ProfileService           Local + XML profile save/load
│     ├─ SafetyRules              Protected-process and risk classification
│     ├─ DeviceOptimizationService Reversible Windows 11 tweaks
│     ├─ ServiceControlService    Allowlisted + app service stop / restore
│     ├─ RespawnTracker           Remembers apps that come back after a boost
│     ├─ LoggerService            Activity, performance, and boost logs
│     ├─ DiagnosticsService       Support-bundle collection
│     ├─ UpdateService            Update checks
│     └─ AdminService             Elevation handling
└─ *.bat                          Build / publish / run helper scripts
```

---

## Disclaimer

Mem-Booster is provided **as is**, with **no warranty** of any kind. Use it at your own risk.

The app is designed to avoid unsafe Windows modifications, but closing apps and changing system settings can still affect running programs, unsaved work, background sync, overlays, notifications, or other active tasks.

**Always save your work before using BOOST NOW or Device Optimise.** Restart Windows if you want to return to a normal background-app state.

---

<div align="center">

Created by **Ox1d3x3** · [Releases](https://github.com/ox1d3x3/mem-booster/releases)

</div>
