@echo off
setlocal
set "LOGDIR=%APPDATA%\Mem-Booster\logs"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
start "" explorer.exe "%LOGDIR%"
