# Mem-Booster v0.4 - native Windows build

Mem-Booster is a native Windows .NET WPF app for creating a repeatable gaming boost profile. Tick the apps you want to close, save the list locally, export it as XML, or load a profile shared by someone else.

Built for: **Ox1d3x3**  
GitHub: **https://github.com/ox1d3x3**

## What changed in v0.4

- Added branded app logo and Windows `.ico` icon.
- Added Ox1d3x3 branding and GitHub link in the UI.
- Added **Select Safe Gaming Apps** for common safe targets like browsers, Widgets, Teams, Game Bar helpers and Xbox background apps.
- Added **Preview Boost** so you can see exactly what will be closed before pressing BOOST.
- Added **Clear Selected**.
- Added **Auto-refresh process list** toggle so the grid does not fight you while selecting apps.
- Added **Smart close first, then force-kill** option.
- Added boost logging to `%APPDATA%\Mem-Booster\logs\boost.log`.
- Improved XML profile save/load and blocked Windows core processes from being saved.
- Improved process-tree close logic and missing-profile-entry reporting.

## Why this version should feel faster

- Native WPF UI instead of Python GUI wrappers.
- No Python runtime.
- Process list is grouped by executable name, so you do not scroll through hundreds of duplicate helper processes.
- Memory refresh runs separately from the process list refresh.
- Virtualised DataGrid for smoother scrolling.
- Native Windows ToolHelp process snapshot for tree-level process termination.
- XML profile export/import for sharing boost profiles.

## Main features

- Real-time system memory display.
- Tick apps and save your local profile.
- Export selected apps to a shareable XML file.
- Load XML profiles from friends.
- Smart skip behaviour: if a profile entry is not running on this PC, Mem-Booster skips it instead of failing.
- Preview mode showing active matches and estimated memory used by matched apps.
- Tree-level boost: closes selected process names and their child/helper processes.
- Built-in safety rules to hide/block core Windows processes.

## Build in Visual Studio

1. Extract the ZIP.
2. Open `MemBooster.sln` in Visual Studio 2022.
3. Make sure the `.NET desktop development` workload is installed.
4. Select `Release`.
5. Press **Build > Build Solution**.
6. Run the app from Visual Studio or publish it.

## Build as a single EXE

Open PowerShell or CMD in this folder and run:

```bat
publish-win-x64.bat
```

The output will be here:

```text
src\MemBooster\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\Mem-Booster.exe
```

This build is self-contained, so it does not require the .NET runtime to be installed on the target PC.

For a smaller EXE that requires the .NET 8 Desktop Runtime, run:

```bat
build-framework-dependent.bat
```

## XML profile format

Example:

```xml
<MemBoosterProfile version="0.4" name="Gaming Boost" author="Ox1d3x3" github="https://github.com/ox1d3x3">
  <Processes>
    <Process executable="msedge.exe" />
    <Process executable="WidgetBoard.exe" />
    <Process executable="GameBar.exe" />
  </Processes>
</MemBoosterProfile>
```

A sample profile is included at:

```text
src\MemBooster\Profiles\gaming-example.xml
```

## Important gaming advice

Do not blindly close Steam, EA App, Riot Client, Battle.net, anti-cheat, fan-control, RGB-control, audio-control, AMD/NVIDIA driver components, or overlay tools if your game depends on them.

For your screenshot, safer targets are usually Edge, Widgets, Microsoft Start Feed, Xbox background apps and other optional background apps. Be more careful with Steam and AMD software.
