@echo off
setlocal
cd /d "%~dp0"

echo Verifying project structure...
if not exist "src\MemBooster\MemBooster.csproj" (
  echo Missing project file.
  exit /b 1
)

dotnet --version
if errorlevel 1 (
  echo dotnet was not found. Install the .NET 8 SDK and Visual Studio 2022 with Windows App SDK tooling.
  exit /b 1
)

echo Building Debug x64...
dotnet build "src\MemBooster\MemBooster.csproj" -c Debug -p:Platform=x64
exit /b %errorlevel%
