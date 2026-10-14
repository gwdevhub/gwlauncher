#!/usr/bin/env bash
# Builds the Windows DLL loader, zips its folder, then one self-contained GW_Launcher binary with the zip embedded.
set -euo pipefail
cd "$(dirname "$0")"

# The same GitVersion calculation the workflow uses, so both platforms report one build number.
cd ..
dotnet tool restore -v q >/dev/null
# The build id is the commit count, carried in the build component of the version: 0.0.<build>.
VERSION="0.0.$(dotnet dotnet-gitversion /showvariable CommitsSinceVersionSource)"
cd - >/dev/null

dotnet publish GwLauncher.Injector/GwLauncher.Injector.csproj -c Release -nologo
python3 - GwLauncher.Injector/bin/Release/net10.0/win-x86 <<'PY'
import os, sys, zipfile
root = sys.argv[1]
publish = os.path.join(root, "publish")
with zipfile.ZipFile(os.path.join(root, "injector.zip"), "w", zipfile.ZIP_DEFLATED) as zip_file:
    for folder, _, files in os.walk(publish):
        for name in files:
            path = os.path.join(folder, name)
            zip_file.write(path, os.path.relpath(path, publish))
PY
dotnet publish GwLauncher.Tray/GwLauncher.Tray.csproj -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:Version="$VERSION" -o Publish -nologo

echo "Published: $(pwd)/Publish/GW_Launcher"
