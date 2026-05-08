@echo off
setlocal
set APPDIR=%APPDATA%\Mem-Booster
if not exist "%APPDIR%" (
  echo No local Mem-Booster data found.
  pause
  exit /b 0
)
set BACKUP=%APPDATA%\Mem-Booster.backup.%date:~-4%%date:~4,2%%date:~7,2%-%time:~0,2%%time:~3,2%%time:~6,2%
set BACKUP=%BACKUP: =0%
echo This will move local profiles/settings/logs to:
echo %BACKUP%
echo.
choice /M "Continue"
if errorlevel 2 exit /b 0
ren "%APPDIR%" "%~nxBACKUP%"
echo Local data reset complete.
pause
