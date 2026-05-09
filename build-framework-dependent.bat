@echo off
setlocal
cd /d "%~dp0"

echo Building Mem-Booster v0.5.22 framework-dependent EXE...
dotnet --version
if errorlevel 1 (
  echo.
  echo ERROR: .NET SDK was not found. Install .NET 8 SDK, then run again.
  pause
  exit /b 1
)

dotnet publish .\src\MemBooster\MemBooster.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=false /p:PublishReadyToRun=false
if errorlevel 1 (
  echo.
  echo ERROR: publish failed.
  pause
  exit /b 1
)

echo.
echo Done. EXE location:
echo .\src\MemBooster\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\Mem-Booster.exe
echo NOTE: this smaller build requires .NET 8 Desktop Runtime on the target PC.
pause
