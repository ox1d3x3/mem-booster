@echo off
setlocal
cd /d "%~dp0"

echo Cleaning old build output...
if exist .\src\MemBooster\bin rmdir /s /q .\src\MemBooster\bin
if exist .\src\MemBooster\obj rmdir /s /q .\src\MemBooster\obj

echo Checking .NET SDK...
dotnet --version
if errorlevel 1 (
  echo.
  echo ERROR: .NET SDK was not found. Install .NET 8 SDK with Desktop workload, then run again.
  pause
  exit /b 1
)

echo.
echo Publishing Mem-Booster v0.5.24 self-contained single EXE...
dotnet publish .\src\MemBooster\MemBooster.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:PublishReadyToRun=false
if errorlevel 1 (
  echo.
  echo ERROR: publish failed.
  pause
  exit /b 1
)

echo.
echo Done. EXE location:
echo .\src\MemBooster\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\Mem-Booster.exe
echo.
pause
