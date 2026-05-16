@echo off
setlocal
cd /d "%~dp0"

echo Building Mem-Booster WinUI 3 v0.6.13 framework-dependent x64 build...
dotnet build "src\MemBooster\MemBooster.csproj" -c Release -p:Platform=x64

pause
