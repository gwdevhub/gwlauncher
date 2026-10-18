# GW Launcher Development

The launcher is a C++ program in `backend/`, with a web page in `frontend/` that it shows in a native window. Windows and Linux builds come from the same sources. Built executables go to `bin/`, which is not committed.

## Layout

| Folder | What's in it |
|---|---|
| `backend/src` | Shared launcher code: accounts, settings, launch steps, issues, the windows' bridge. |
| `backend/platform/windows`, `backend/platform/linux` | The platform layer: windows, desktop, launching the game, networking, tray, shortcuts. |
| `backend/include` | Headers for the shared code and the platform layer. |
| `backend/third_party` | Vendored code, each library with its own `LICENSE.txt`. The GTK and WebKitGTK headers are stubs, so the Linux build needs none of their development packages. |
| `backend/cmake` | Build helpers: the page embedding, gzip, and the MinGW and zig toolchains. |
| `backend/packaging` | The AppImage script and the zig compiler wrappers. |
| `frontend` | The web page. `frontend/dist` is compiled in, so it's committed with its sources. |
| `assets` | Icons and the stylesheet template, compiled in as well. |
| `bin` | Build output: `bin/linux/{debug,release}` and `bin/windows`. Ignored by git. |

## Build presets

`backend/CMakePresets.json` defines the builds. Run the presets from `backend/`.

| Preset | Output | Build folder |
|---|---|---|
| `linux-debug` | `bin/linux/debug/gwlauncher` | `backend/build-dev` |
| `linux-release` | `bin/linux/release/GW_Launcher` | `backend/build` |
| `linux-release-zig` | `bin/linux/release/GW_Launcher`, built against glibc 2.35 | `backend/build-zig` |
| `windows-release` | `bin/windows/GW_Launcher.exe` and `GW_Launcher.cli.exe` | `backend/build-win` |

```
cd backend
cmake --preset linux-release
cmake --build --preset linux-release
```

The presets use Ninja. Build folders are ignored by git.

### The web page

Build the page before the launcher, since CMake skips the launcher target when `frontend/dist/index.html` is missing:

```
cd frontend
npm ci
npm run build
```

The native build embeds `frontend/dist/index.html`, gzip-compressed. The `native` workflow rebuilds the page and fails if the committed `dist` differs, so commit it with the source changes that produced it.

### Linux

The Linux build needs:
- CMake 3.20 or newer, and Ninja
- A C++20 compiler: GCC or Clang
- MinGW-w64 (`x86_64-w64-mingw32-g++`). The Linux launcher carries `GW_Launcher.cli.exe`, which is a Windows program built with MinGW during the build.
- Optional: `ccache`, which CMake uses when it's found

Packages by distribution:
- Debian and Ubuntu: `cmake ninja-build g++ mingw-w64`
- Fedora: `cmake ninja-build gcc-c++ mingw64-gcc-c++`
- Arch: `cmake ninja gcc mingw-w64-gcc`

The launcher needs GTK 3 and WebKitGTK 4.1 at runtime. It opens them with `dlopen`, so there are no development packages to install, and a missing library shows a message instead of stopping the launch. Install the runtime packages to run it:
- Debian and Ubuntu: `libgtk-3-0 libwebkit2gtk-4.1-0`
- Fedora: `gtk3 webkit2gtk4.1`
- Arch: `gtk3 webkit2gtk-4.1`

Wine runs Guild Wars. The launcher offers to install it through the distribution's package manager, through `pkexec`. Steam accounts use Proton's Wine.

### Windows

`windows-release` cross-compiles with MinGW from Linux (`backend/cmake/toolchains/mingw-w64.cmake`). The native Windows build is not supported. The WebView2 headers are in `backend/third_party/webview2`, and `WebView2Loader.dll` is loaded at runtime.

```
cd backend
cmake --preset windows-release
cmake --build --preset windows-release
```

### Windows build in Docker

`backend/packaging/docker` has an Alpine image with CMake, Ninja and the MinGW toolchain, so the Windows build runs the same on Linux and Windows hosts. Docker needs to be running; the repository is mounted into the container and the output lands in `bin/windows`.

```
backend/packaging/docker/build.sh               # Linux or Git Bash
powershell backend/packaging/docker/build.ps1   # Windows
```

The container builds into `backend/build-docker`, so it never shares a CMake cache with a host build. The image builds only `windows-release`: the Linux release links against glibc, and Alpine uses musl. Build Linux on a Linux host with the presets above.

## Running

- `backend/run-linux.sh` runs the release build with its data in `~/.local/share/gwlauncher-native`, separate from a normal install.
- `backend/dev.sh` runs the debug build and serves the page from Vite, so edits under `frontend/src` show without a rebuild. It needs `npm ci` in `frontend` first.

## Packaging

- `backend/packaging/appimage/build-appimage.sh` packs `bin/linux/release/GW_Launcher` into `bin/linux/appimage/GW_Launcher-x86_64.AppImage`. It needs `appimagetool` on `PATH`, or `APPIMAGETOOL` set to its path. The AppImage uses the host's GTK 3 and WebKitGTK 4.1.
- `linux-release-zig` builds against glibc 2.35, so the binary runs on older distributions. It needs `zig` on `PATH`, or `ZIG` set to its path.

## CI

`.github/workflows/native.yml` runs on changes under `backend`, `frontend`, `assets`, and the workflow file:
- `linux` checks the committed page, then builds `linux-release` and uploads `bin/linux/release/GW_Launcher`.
- `windows` checks the committed page, then builds `windows-release` and uploads `bin/windows/GW_Launcher.exe` and `GW_Launcher.cli.exe`.

## Environment overrides

- `GWLAUNCHER_HELPER`: the Windows command-line launcher to run under Wine, instead of the one built into the Linux launcher.
- `GWLAUNCHER_WINE`: the Wine binary to use.
- `GWL_DEV_URL`: the page's address in a debug run, set by `dev.sh`.

## Linux data

The launcher keeps its files in `$XDG_DATA_HOME/gwlauncher` (default `~/.local/share/gwlauncher`):
- `Accounts.json`, `Settings.json`
- `prefix/`: the Wine prefix
- `gwlauncher.sock`: the socket that a second start uses to reach the running launcher
- `steam-favicon.ico`: Steam's favicon, fetched from `store.steampowered.com` when a Steam account is first shown. Steam accounts show the helmet until it's there, and when it can't be fetched

`Settings.json` can set `SteamPath` (the Steam client's folder, the one with `legacycompat`) and `WinePath` (the Wine program) by hand. Empty means the launcher finds them, and the settings window shows what it found as the placeholder.

Accounts launch by running `GW_Launcher.cli.exe` under Wine with `--launch`.
