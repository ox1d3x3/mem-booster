@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "src\MemBooster\MemBooster.csproj" -c Debug -p:Platform=x64
pause
