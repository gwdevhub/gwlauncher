# Path icons and game shortcuts

`pathIcon(path)` is a generic frontend request. The backend resolves a native or Wine path on an icon worker and replies with PNG bytes as a JSON number array, or `null` when no supported image is available. Account replies contain `iconPath` plus an immediate fallback image. `PathIcon` deduplicates in-flight requests and releases its blob URLs on replacement/unmount.

PNG, ICO, PE32 and PE32+ resource icons use the existing image readers. The process-owned cache is keyed by canonical image path and resource index, survives all window lifetimes, and refreshes changed files by modification time. Optional icon work has its own worker pool.

## Shortcut resolution

Both icon lookup and launching use `resolveShortcut`:

- `.lnk`: reuse the existing MS-SHLLINK reader for filesystem targets and arguments, with relative/working-directory resolution and custom icon locations. Positive icon indexes select a resource group; negative indexes name a resource ID. Unsupported non-filesystem links report an error.
- `.desktop`: read the `[Desktop Entry]` Application entry, tokenize its quoted `Exec`, resolve `Path` and relative/bare executable names, and use `Icon` when available. PNG/ICO hicolor and pixmap names are searched through XDG data directories. Missing custom icons fall back to the target's icon.
- Desktop field codes `%c`, `%k`, `%i` and `%%` are expanded. Standalone `%f`, `%F`, `%u` and `%U` are omitted because no documents/URLs are supplied. Other field codes report an error rather than being passed literally.
- A direct `wine`/`wine64` prefix is accepted. Game launches must ultimately target a client `.exe`; shell scripts, environment-wrapper commands, terminal entries and application-launcher indirection are not executed as game clients. GW Launcher retains its configured Wine/Steam runtime and normal client working directory.
- Shortcut chains share one cycle/depth check (maximum 16). Nested arguments precede outer shortcut arguments, followed by the account's extra arguments. The original shortcut path remains in Accounts.json.
- Status rendering uses only the last worker-resolved shortcut target. It does not parse shortcut files on the UI thread.

Windows argument quoting is shared by shortcut arguments, process creation and game login arguments, including embedded quotes and trailing backslashes.

## Verification checklist

Static checks do not replace the following Windows/Linux runtime checks; no build is implied by this checklist.

- Fetch a PNG, ICO, PE32 and PE32+ icon; concurrent requests for one image/resource must share extraction.
- Recreate the list/progress WebView and request an icon again: the backend cache must remain populated, and old blob URLs must be released.
- Test a `.lnk` with an absolute target, a relative target plus working directory, custom ICO/PE icons and positive/negative icon indexes.
- Test a `.desktop` with quoted Unicode/spaced paths, direct and Wine-prefixed Exec, relative Path, theme Icon, and each supported field code.
- Launch through either shortcut type: preserve shortcut/account arguments, apply issue checks and mod loading to the resolved client, and show Active/In Use for shared targets.
- Broken targets, truncated links, malformed quotes, cycles and excessive chains must show a useful failure; optional icon failures must retain the fallback.
- Modify the shortcut's target/Icon and then refresh; modify the image file and request it again; neither case may return the old image indefinitely.
- Test empty arguments, embedded quotes and paths ending in backslashes through both direct Windows launches and the Wine helper.
