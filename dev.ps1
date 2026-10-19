# Rebuilds changed native inputs and runs the launcher against Vite. Ctrl+C stops both.
param([ValidateRange(1, 65535)][int]$Port = 5173)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$frontend = Join-Path $root 'frontend'
$launcherFolder = Join-Path $root 'bin\windows'
$launcherExe = Join-Path $launcherFolder 'GW_Launcher.exe'
$helperExe = Join-Path $launcherFolder 'GW_Launcher.cli.exe'
$buildStateFile = Join-Path $launcherFolder '.dev-build.json'
$devUrl = "http://127.0.0.1:$Port"

function Get-InputHash([string[]]$Paths) {
    $files = foreach ($relative in $Paths) {
        Get-ChildItem -LiteralPath (Join-Path $root $relative) -Recurse -File
    }
    $entries = foreach ($file in ($files | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($root.Length + 1)
        "$relative $((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)"
    }
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        [Convert]::ToBase64String($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes(($entries -join "`n"))))
    } finally {
        $hash.Dispose()
    }
}

function Get-BinaryHash([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }
}

function Stop-DevProcess([Diagnostics.Process]$Process, [switch]$Tree) {
    if ($null -eq $Process) { return }
    try {
        if ($Process.HasExited) { return }
        if ($Tree) {
            $killer = New-Object Diagnostics.Process
            $killer.StartInfo.FileName = Join-Path $env:SystemRoot 'System32\taskkill.exe'
            $killer.StartInfo.Arguments = "/PID $($Process.Id) /T /F"
            $killer.StartInfo.UseShellExecute = $false
            $killer.StartInfo.CreateNoWindow = $true
            try {
                $null = $killer.Start()
                if (-not $killer.WaitForExit(5000)) {
                    $killer.Kill()
                    throw "Timed out stopping process $($Process.Id)."
                }
            } finally {
                $killer.Dispose()
            }
        } else {
            $Process.Kill()
        }
        if (-not $Process.WaitForExit(5000)) {
            throw "Process $($Process.Id) did not stop."
        }
    } catch [InvalidOperationException] {
        # Starting can fail before the process has an ID, or it can exit during cleanup.
    } finally {
        $Process.Dispose()
    }
}

$running = @(Get-CimInstance Win32_Process -Filter "Name='GW_Launcher.exe'" |
    Where-Object { $_.ExecutablePath -eq $launcherExe })
if ($running.Count -gt 0) { throw 'Close the running launcher before starting dev.ps1.' }

$buildState = @{
    shared = Get-InputHash @('backend/src', 'backend/include', 'backend/cmake', 'backend/third_party', 'assets',
        'backend/CMakeLists.txt', 'backend/CMakePresets.json', 'build.ps1', 'Dockerfile', 'Dockerfile.dockerignore',
        'Dockerfile.toolchain', 'Dockerfile.toolchain.dockerignore')
    windows = Get-InputHash @('backend/platform/windows')
    launcher = Get-BinaryHash $launcherExe
    helper = Get-BinaryHash $helperExe
}
$previousBuild = $null
if (Test-Path -LiteralPath $buildStateFile) {
    try { $previousBuild = Get-Content -LiteralPath $buildStateFile -Raw | ConvertFrom-Json } catch {}
}
$rebuild = -not $previousBuild -or -not $buildState.launcher -or -not $buildState.helper
foreach ($key in $buildState.Keys) {
    if ($previousBuild.$key -ne $buildState[$key]) { $rebuild = $true }
}
if ($rebuild) {
    $target = if (-not $previousBuild -or $previousBuild.shared -ne $buildState.shared) { 'all' } else { 'windows' }
    Write-Host "Rebuilding the native launcher ($target)..."
    & (Join-Path $root 'build.ps1') -Target $target
    if ($LASTEXITCODE -ne 0) { throw 'The native build failed.' }
    $buildState.launcher = Get-BinaryHash $launcherExe
    $buildState.helper = Get-BinaryHash $helperExe
    if (-not $buildState.launcher -or -not $buildState.helper) { throw 'The build did not produce the launcher and helper.' }
    $buildState | ConvertTo-Json | Set-Content -LiteralPath $buildStateFile -Encoding UTF8
}

Push-Location $frontend
try {
    if (-not (Test-Path 'node_modules/vite/bin/vite.js')) {
        npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'The frontend packages failed to install.' }
    }
} finally {
    Pop-Location
}

$probe = New-Object Net.Sockets.TcpListener ([Net.IPAddress]::Loopback), $Port
try {
    $probe.Start()
} catch {
    throw "Port $Port is already in use. Stop that server or run dev.ps1 -Port <another port>."
} finally {
    $probe.Stop()
}

$vite = New-Object Diagnostics.Process
$launcher = New-Object Diagnostics.Process
try {
    $vite.StartInfo.FileName = (Get-Command node.exe -ErrorAction Stop).Source
    $viteEntry = Join-Path $frontend 'node_modules\vite\bin\vite.js'
    $vite.StartInfo.Arguments = "`"$viteEntry`" --host 127.0.0.1 --port $Port --strictPort"
    $vite.StartInfo.WorkingDirectory = $frontend
    $vite.StartInfo.UseShellExecute = $false
    $vite.StartInfo.CreateNoWindow = $true
    $null = $vite.Start()

    $ready = $false
    for ($i = 0; $i -lt 30 -and -not $ready; $i++) {
        if ($vite.HasExited) { throw 'The Vite dev server exited.' }
        try {
            Invoke-WebRequest -Uri $devUrl -UseBasicParsing -TimeoutSec 1 | Out-Null
            $ready = $true
        } catch {
            Start-Sleep -Milliseconds 200
        }
    }
    if (-not $ready) { throw "The Vite dev server didn't answer at $devUrl." }

    $launcher.StartInfo.FileName = $launcherExe
    $launcher.StartInfo.WorkingDirectory = $launcherFolder
    $launcher.StartInfo.UseShellExecute = $false
    $launcher.StartInfo.EnvironmentVariables['GWL_DEV_URL'] = $devUrl
    $null = $launcher.Start()
    Write-Host "Launcher running against $devUrl. Press Ctrl+C or close the launcher to stop both."
    while (-not $launcher.WaitForExit(200)) {
        if ($vite.HasExited) { throw 'The Vite dev server exited.' }
    }
} finally {
    try {
        Stop-DevProcess $launcher
    } finally {
        Stop-DevProcess $vite -Tree
    }
}
