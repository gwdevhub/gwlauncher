# Builds the launcher in Docker into bin/: build.ps1 [-Target linux|windows|all]
param([ValidateSet('linux', 'windows', 'all')][string]$Target = 'all')

$ErrorActionPreference = 'Stop'
$presets = switch ($Target) {
    'linux' { 'linux-release' }
    'windows' { 'windows-release' }
    'all' { 'linux-release windows-release' }
}
$root = $PSScriptRoot

# Before anything is built: a launcher running from bin locks its exe, and the build needs to write there.
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$running = @(Get-CimInstance Win32_Process -Filter "Name='GW_Launcher.exe'" |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith("$bin\", [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { $_.ProcessId })
if ($running.Count -gt 0) {
    throw "Close the launcher running from $bin first (process $($running -join ', ')): its exe is locked, so the build can't replace it."
}
$probe = Join-Path $bin ".build-write-test-$PID"
try {
    [IO.File]::WriteAllText($probe, 'x')
    Remove-Item $probe -Force
} catch {
    throw "$bin can't be written to, so the build can't put its output there. Check the folder's permissions."
}
# Empty .tmp files are left when an export can't rename its output over a locked exe.
Get-ChildItem $bin -Recurse -Force -File -Filter '.tmp.*' | Where-Object { $_.Length -eq 0 } | Remove-Item -Force

if (-not (Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue)) {
    Start-Process (Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe')
}
$started = $false
for ($i = 0; $i -lt 90 -and -not $started; $i++) {
    docker info *> $null
    $started = $LASTEXITCODE -eq 0
    if (-not $started) { Start-Sleep -Seconds 2 }
}
if (-not $started) { throw "Docker didn't start." }

# The page is embedded into the launcher, so it's built here rather than in the container.
Push-Location (Join-Path $root 'frontend')
try {
    npm.cmd install
    if ($LASTEXITCODE -ne 0) { throw "The page's packages failed to install." }
    npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'The page build failed.' }
} finally {
    Pop-Location
}

# The builder image is tagged by its Dockerfile.toolchain hash, so the compilers are only installed again when that file changes.
$builderHash = (Get-FileHash (Join-Path $root 'Dockerfile.toolchain') -Algorithm SHA256).Hash.Substring(0, 12).ToLower()
$builderImage = "gwdevhub-builder:$builderHash"
docker image inspect $builderImage *> $null
if ($LASTEXITCODE -ne 0) {
    docker build -t $builderImage -f (Join-Path $root 'Dockerfile.toolchain') $root
    if ($LASTEXITCODE -ne 0) { throw 'The builder image failed to build.' }
    # Older builder images are dropped so each change doesn't leave another one behind.
    docker image ls gwdevhub-builder --format '{{.Repository}}:{{.Tag}}' | Where-Object { $_ -ne $builderImage } | ForEach-Object { docker rmi $_ *> $null }
}

# The build number comes from the host's git history, because the container doesn't copy .git.
$rev = git -C $root rev-list --count HEAD

docker build --target export --output "type=local,dest=$root\bin" `
    --build-arg "BUILDER_IMAGE=$builderImage" --build-arg "PRESETS=$presets" --build-arg "GWL_BUILD_NUMBER=$rev" `
    -f (Join-Path $root 'Dockerfile') $root
if ($LASTEXITCODE -ne 0) { throw 'The container build failed.' }

"Built $presets into $root\bin"
