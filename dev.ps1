# Serves the page from the Vite dev server and runs bin/windows/GW_Launcher.exe against it, so frontend edits show on save.
# Build the launcher once with build.ps1 first. Closing the launcher stops the dev server.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$frontend = Join-Path $root 'frontend'
$launcherExe = Join-Path $root 'bin\windows\GW_Launcher.exe'
$devUrl = 'http://127.0.0.1:5173'

if (-not (Test-Path $launcherExe)) { throw "$launcherExe is missing. Run build.ps1 first." }

Push-Location $frontend
try {
    if (-not (Test-Path 'node_modules/.bin/vite.cmd')) { npm.cmd ci }
} finally {
    Pop-Location
}

$vite = Start-Process -FilePath 'npm.cmd' -ArgumentList 'run', 'dev' -WorkingDirectory $frontend -WindowStyle Minimized -PassThru

try {
    $ready = $false
    for ($i = 0; $i -lt 30 -and -not $ready; $i++) {
        try {
            Invoke-WebRequest -Uri $devUrl -UseBasicParsing -TimeoutSec 2 | Out-Null
            $ready = $true
        } catch {
            if ($vite.HasExited) { throw 'The Vite dev server exited. Check that port 5173 is free.' }
            Start-Sleep -Seconds 1
        }
    }
    if (-not $ready) { throw "The Vite dev server didn't answer at $devUrl." }

    $env:GWL_DEV_URL = $devUrl
    $launcher = Start-Process -FilePath $launcherExe -PassThru
    "Launcher running against $devUrl. Close it to stop the dev server."
    $launcher.WaitForExit()
} finally {
    Remove-Item Env:GWL_DEV_URL -ErrorAction SilentlyContinue
    if (-not $vite.HasExited) {
        # npm starts vite as a child, so the whole tree goes.
        & taskkill.exe /T /F /PID $vite.Id | Out-Null
    }
}
