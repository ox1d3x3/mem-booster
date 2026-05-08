@echo off
setlocal
set LOG=%APPDATA%\Mem-Booster\logs\startup.log
if not exist "%LOG%" (
  echo Startup log does not exist yet:
  echo %LOG%
  echo.
  pause
  exit /b 1
)
notepad "%LOG%"
