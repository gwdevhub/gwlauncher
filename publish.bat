@echo off
setlocal
cd /d "%~dp0"

rem Builds both executables the way the GitHub Actions workflow does. Publish needs Visual Studio's MSBuild.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (echo vswhere not found. Install Visual Studio with the .NET desktop workload. & exit /b 1)

set "MSBUILD="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
if not defined MSBUILD (echo MSBuild not found. & exit /b 1)

rem The version comes from GitVersion, the same calculation the workflow uses.
dotnet tool restore >nul || exit /b 1
set "BUILD_COMMITS="
for /f %%v in ('dotnet dotnet-gitversion /showvariable CommitsSinceVersionSource') do set "BUILD_COMMITS=%%v"
if not defined BUILD_COMMITS (echo GitVersion didn't return a commit count. & exit /b 1)
rem The build id is the commit count, carried in the build component of the version: 0.0.<build>.
set "BUILD_VERSION=0.0.%BUILD_COMMITS%"

echo Building the self-contained executable %BUILD_VERSION%...
"%MSBUILD%" "GW Launcher\GW Launcher.csproj" /target:"Restore;Publish" /m /nodeReuse:false /property:"PublishProfile=SelfContained" /property:GitVersion_NoFetchEnabled=true /property:"Version=%BUILD_VERSION%" || exit /b 1
move /Y "Publish\GW Launcher.exe" "Publish\GW_Launcher.exe" >nul || exit /b 1

echo Building the framework-dependent executable %BUILD_VERSION%...
"%MSBUILD%" "GW Launcher\GW Launcher.csproj" /target:"Restore;Publish" /m /nodeReuse:false /property:"PublishProfile=FrameworkDependent" /property:GitVersion_NoFetchEnabled=true /property:"Version=%BUILD_VERSION%" || exit /b 1
move /Y "Publish\GW Launcher.exe" "Publish\GW_Launcher_Framework_Dependent.exe" >nul || exit /b 1

echo.
echo Done. Publish\GW_Launcher.exe needs no install. Publish\GW_Launcher_Framework_Dependent.exe needs the .NET 10 Desktop Runtime.
