@echo off
setlocal
cd /d "%~dp0"

echo Cleaning previous build output...
if exist "src\MemBooster\bin" rmdir /s /q "src\MemBooster\bin"
if exist "src\MemBooster\obj" rmdir /s /q "src\MemBooster\obj"

echo Publishing Mem-Booster WinUI 3 v0.6.14 self-contained x64 build...
dotnet publish "src\MemBooster\MemBooster.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:EnableMsixTooling=true

echo.
echo Output:
echo src\MemBooster\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\Mem-Booster.exe
pause
