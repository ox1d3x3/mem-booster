# Mem-Booster by Ox1d3x3 v0.6.13 WinUI 3

This is the first WinUI 3 migration build of Mem-Booster.

Mem-Booster is a native Windows 11 utility for preparing a cleaner gaming session. It helps close selected non-gaming background apps, apply optional reversible Windows 11 gaming-session settings, and keep logs for troubleshooting.

## Why WinUI 3?

The v0.6.13 branch moves the UI from WPF to WinUI 3 for a more modern Windows 11 interface. The core process logic, safety rules, profiles, logging, Device Optimise logic and boost behaviour were kept close to the stable v0.5.x branch.

## Important note

This is a migration build. It should be tested separately from the stable WPF build before replacing it.

## Requirements

To run the published self-contained build:

- Windows 11 x64

To build from source:

- Windows 11
- Visual Studio 2022
- .NET 8 SDK
- Windows App SDK / WinUI 3 tooling
- .NET desktop development workload

The project references:

```text
Microsoft.WindowsAppSDK
```

## How to use

1. Open Mem-Booster.
2. The running app list is scanned once at startup.
3. Select apps manually or use Safe Select, Extreme Select, or Aggressive Select.
4. Review the Selected apps panel.
5. Optional: choose Device Optimise and select the Windows 11 settings you want.
6. Click BOOST NOW.
7. Restart Windows after a heavy boost if you want all background apps and helpers to return normally.
8. Use Revert Device Optimise for Windows setting changes applied through Device Optimise.

## Manual refresh

The running app list does not auto-refresh. This avoids the list changing while you are selecting apps.

Use:

```text
Refresh Running Apps
```

when you want to rescan the current running app list.

## Safety approach

Mem-Booster avoids core Windows, antivirus/security, VPN/firewall, GPU driver/control, RGB/fan-control, game launcher, anti-cheat, Discord, MSI Afterburner and RivaTuner related processes.

Aggressive Select focuses on broad non-gaming software such as browsers, Office/Microsoft 365, Adobe/Creative Cloud, Autodesk/AutoCAD/Revit/Inventor/Fusion/Maya/3ds Max, Bentley/MicroStation/OpenRoads/ProjectWise, cloud sync tools, dev tools and productivity helpers.

## Disclaimer

Mem-Booster is provided as is, with no warranty of any kind.

Use it at your own risk. Always save your work before using BOOST NOW or Device Optimise.

## Build

Run:

```bat
verify-build.bat
```

Publish self-contained x64:

```bat
clean-publish-win-x64.bat
```

Output:

```text
src\MemBooster\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\Mem-Booster.exe
```
