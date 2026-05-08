@echo off
setlocal
cd /d "%~dp0"
set EXE=.\src\MemBooster\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\Mem-Booster.exe
if not exist "%EXE%" (
  echo Published EXE not found.
  echo Run clean-publish-win-x64.bat first.
  pause
  exit /b 1
)
echo Starting Mem-Booster...
"%EXE%"
echo.
echo App exited with code: %ERRORLEVEL%
echo Startup log: %APPDATA%\Mem-Booster\logs\startup.log
echo.
pause
