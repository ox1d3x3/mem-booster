@echo off
setlocal
cd /d "%~dp0"
set LOGDIR=%APPDATA%\Mem-Booster\logs
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set LOG=%LOGDIR%\diagnose-startup.log

echo ================================================== > "%LOG%"
echo Mem-Booster startup diagnostic >> "%LOG%"
echo Time: %date% %time% >> "%LOG%"
echo Folder: %cd% >> "%LOG%"
echo ================================================== >> "%LOG%"
echo. >> "%LOG%"

echo Checking .NET SDK... >> "%LOG%"
dotnet --info >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo Running Debug build from source... >> "%LOG%"
dotnet run --project .\src\MemBooster\MemBooster.csproj -c Debug >> "%LOG%" 2>&1
set EXITCODE=%ERRORLEVEL%

echo. >> "%LOG%"
echo Exit code: %EXITCODE% >> "%LOG%"
echo Diagnostic log saved to: %LOG%
echo.
echo If the app still does not open, send this file:
echo %LOG%
echo.
pause
