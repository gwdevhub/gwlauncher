# Guidelines

## Comments

- Comment only when it adds something the code doesn't already say — explain *why*, not *what*. Skip comments that just restate the next line.
- When a comment is needed, keep it to a single line and concise. Do not write multi-line comment blocks of prose.
- The single-line rule does not apply to structural section banners (`// ===`/title/`// ===`) or to license/file headers.

## Building

Build with the scripts at the repository root, not the CMake presets or Docker directly:

- `./build.sh [linux|windows|all]` from Git Bash, or `./build.ps1 -Target linux|windows|all` from PowerShell.
- Pick the target by what changed:
  - `windows`: changes under `backend/platform/windows` or other Windows-only code.
  - `linux`: changes under `backend/platform/linux` or other Linux-only code.
  - `all`: changes to shared code (`backend/src`, `backend/include`, `backend/CMakeLists.txt`, `backend/cmake`, `assets`), or before handing work off.
- The scripts start Docker if it isn't running, rebuild `frontend/dist`, and write the results to `bin/`. Don't build `frontend` or run `docker` by hand for a build.
- Don't commit `bin/`, `frontend/dist`, or `backend/build*` output.
