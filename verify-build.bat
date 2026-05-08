@echo off
setlocal
cd /d "%~dp0"
echo Verifying Mem-Booster source with dotnet...
echo.
dotnet --info
if errorlevel 1 goto :fail

echo.
echo Restoring packages...
dotnet restore src\MemBooster\MemBooster.csproj
if errorlevel 1 goto :fail

echo.
echo Building Release...
dotnet build src\MemBooster\MemBooster.csproj -c Release --no-restore
if errorlevel 1 goto :fail

echo.
echo Verify build passed.
pause
exit /b 0

:fail
echo.
echo Verify build failed. Copy the error output and send it back.
pause
exit /b 1
