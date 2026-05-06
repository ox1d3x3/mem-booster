@echo off
setlocal
cd /d "%~dp0"

echo Building Mem-Booster v0.4 native Windows EXE...
dotnet publish .\src\MemBooster\MemBooster.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:PublishReadyToRun=true

echo.
echo Done. EXE location:
echo .\src\MemBooster\bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\Mem-Booster.exe
pause
