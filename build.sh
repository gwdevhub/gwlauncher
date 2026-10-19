#!/usr/bin/env bash
# Builds the launcher in Docker into bin/: build.sh [linux|windows|all]
set -euo pipefail

target="${1:-all}"
case "$target" in
    linux) presets="linux-release" ;;
    windows) presets="windows-release" ;;
    all) presets="linux-release windows-release" ;;
    *) echo "usage: build.sh [linux|windows|all]" >&2; exit 2 ;;
esac

root="$(cd "$(dirname "$0")" && pwd)"
host_root="$root"
windows_host=false
case "$(uname -s)" in MINGW*|MSYS*) windows_host=true ;; esac
if $windows_host; then
    # Git Bash rewrites paths unless conversion is off, and Docker needs the Windows form of the repository path.
    export MSYS_NO_PATHCONV=1
    host_root="$(cygpath -m "$root")"
fi

# Before anything is built: a launcher running from bin locks its exe, and the build needs to write there.
preflight() {
    local bin="$root/bin"
    mkdir -p "$bin"
    if $windows_host; then
        local bin_win running
        bin_win="$(cygpath -w "$bin")"
        running="$(powershell.exe -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='GW_Launcher.exe'\" | Where-Object { \$_.ExecutablePath -like '$bin_win\*' } | ForEach-Object { \$_.ProcessId }" | tr -d '
' | tr '
' ' ')"
        if [ -n "$running" ]; then
            echo "Close the launcher running from $bin_win first (process $(echo $running)): its exe is locked, so the build can't replace it." >&2
            exit 1
        fi
    elif pgrep -x GW_Launcher >/dev/null; then
        echo "Close the running gwlauncher first: the build can't replace it." >&2
        exit 1
    fi
    local probe="$bin/.build-write-test-$$"
    if ! : > "$probe" 2>/dev/null; then
        echo "$bin can't be written to, so the build can't put its output there. Check the folder's permissions." >&2
        exit 1
    fi
    rm -f "$probe"
    # Empty .tmp files are left when an export can't rename its output over a locked exe.
    find "$bin" -type f -name '.tmp.*' -empty -delete
}

preflight

if ! docker info >/dev/null 2>&1; then
    if ! $windows_host; then
        echo "Docker isn't running: start the Docker daemon and run this again." >&2
        exit 1
    fi
    powershell.exe -NoProfile -Command 'Start-Process (Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe")'
    for _ in $(seq 1 90); do docker info >/dev/null 2>&1 && break; sleep 2; done
    docker info >/dev/null 2>&1 || { echo "Docker didn't start." >&2; exit 1; }
fi

# The page is embedded into the launcher, so it's built here rather than in the container.
(
    cd "$root/frontend"
    npm install
    npm run build
)

# The builder image is tagged by its Dockerfile.toolchain hash, so the compilers are only installed again when that file changes.
builder_hash="$(sha256sum "$root/Dockerfile.toolchain" | cut -c1-12)"
builder_image="gwdevhub-builder:$builder_hash"
if ! docker image inspect "$builder_image" >/dev/null 2>&1; then
    docker build -t "$builder_image" -f "$host_root/Dockerfile.toolchain" "$host_root"
    # Older builder images are dropped so each change doesn't leave another one behind.
    docker image ls gwdevhub-builder --format '{{.Repository}}:{{.Tag}}' | { grep -v ":$builder_hash\$" || true; } | xargs -r docker rmi >/dev/null
fi

# The build number comes from the host's git history, because the container doesn't copy .git.
rev="$(git -C "$host_root" rev-list --count HEAD)"

docker build --target export --output "type=local,dest=$host_root/bin" \
    --build-arg BUILDER_IMAGE="$builder_image" --build-arg PRESETS="$presets" --build-arg GWL_BUILD_NUMBER="$rev" \
    -f "$host_root/Dockerfile" "$host_root"

echo "Built $presets into $root/bin"
