@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1 || (echo dotnet SDK not found. Install .NET 10 SDK. & exit /b 1)

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (echo vswhere not found. Install Visual Studio with the .NET desktop workload. & exit /b 1)

rem The COM reference (IWshRuntimeLibrary) needs Visual Studio's MSBuild, not dotnet build
set "MSBUILD="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
if not defined MSBUILD (echo MSBuild not found. & exit /b 1)

echo Restoring dotnet tools...
dotnet tool restore || exit /b 1

echo Restoring packages...
"%MSBUILD%" "GW Launcher.sln" /t:Restore /v:m /nologo || exit /b 1

echo.
echo Done. Open GW Launcher.sln, or build with:
echo   "%MSBUILD%" "GW Launcher.sln" /p:Configuration=Release /p:Platform=x86
