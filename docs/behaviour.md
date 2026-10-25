# GW Launcher behaviour

What the launcher should do in each situation. Windows is the reference. Each statement says what happens; the
**Linux gap** notes list where the Linux build currently differs, with the reason in the code.

Statements marked *(to confirm)* describe intended behaviour that the code doesn't settle.

## Starting the launcher

- Only one instance of the launcher runs at a time.
- Starting the launcher while one is running doesn't start another. The running instance shows its account list instead.
- In tray mode the list is shown beside the tray icon, as a left click does. Otherwise it's shown and brought to the front, restored if minimised.
- This includes the desktop shortcuts that start an account (`-launch <name>`). The running instance launches that account, and the second start exits.

**Linux:** the second start passes its request over a socket in the data directory (`gwlauncher.sock`). A socket left by a launcher that exited doesn't stop the next start.
**Windows:** the second start passes its request over a named pipe for the user (`GwLauncher-<user>`).

## Tray mode and the account list

*Tray mode is the setting Keep in system tray.*

- In tray mode the account list has no taskbar button, and no title bar unless Show title bar in tray mode is on (it's on by default).
- Left-clicking the tray icon shows the list beside the icon when it's hidden, and hides it when it's shown.
- When the list opens from the tray, it sits centred on the icon, above the icon when the icon is in the bottom half of the screen, and below it otherwise. It stays inside the screen.
- Closing the list (its close button or Escape) hides it. The launcher keeps running. With Show title bar in tray mode on, closing the list quits the launcher instead.
- When focus leaves the shown list, the list hides, unless the pointer is over it.
- A list that has just been shown ignores focus loss for 400 ms, so the click that opened it can't close it.
- With Launch minimized on, the list stays hidden at startup. Otherwise it opens at startup.
- The tray icon's menu has Open GW Launcher (toggles the list) and Quit (exits the launcher).

**Linux gap:** the list opens beside the click point, not the icon's rectangle. The desktop's tray doesn't report the rectangle.

## Not in tray mode

- The list has a title bar and a taskbar button.
- The list is always visible at startup, even if it was hidden before.
- Closing the list minimises it to the taskbar. It doesn't hide.

## Changing the tray setting

- Turning tray mode on adds the tray icon and removes the title bar and taskbar button.
- Turning tray mode off removes the tray icon, restores the title bar and taskbar button, and shows the list if it was hidden.
- Where no tray exists, Keep in system tray and Launch minimized are forced off. On Windows the launcher checks for a tray first.

**Linux:** a tray exists when a StatusNotifierItem watcher owns `org.kde.StatusNotifierWatcher` on the session bus.

## Menus and popups

- Right-clicking anywhere in the account list opens the context menu at the pointer.
- If an account is under the pointer, the menu starts with its account items (edit, move, remove, launch, shortcut, issues), then the app items.
- Picking a menu item runs the item and closes the menu.
- A menu closes when focus leaves it.
- A menu closed without a pick hides the list, unless the pointer is over it.
- Closing a progress window cancels its job.
- Closing a choice window without answering counts as no choice.
- Closing any popup refreshes the account list.

**Linux gap:** the context menu opens at a default position, because the pointer position isn't read on Linux.

## Accounts list

- Each row shows the account's name, icon, state, and any issues.
- Arrow keys move the selection.
- Enter launches the selected account. Ctrl+Enter launches it suspended.
- Double-click launches an account. Ctrl+double-click launches it suspended.
- Delete asks "Are you sure you want to remove '<name>'?" and removes the account on Yes.
- Escape hides the list when it's shown as the list. From another view, Escape returns to the list.

## Launching

- A blocking issue stops a launch from the list. The launcher shows the account's issues instead.
- A warning doesn't stop a launch.
- An out-of-date Gw.exe is offered for update before launch. Launch stays available.
- A suspended launch holds the game until the user chooses Resume or Cancel. Cancel loads nothing.
- Mods load in order after the game is suspended. After resuming, the launcher waits for its main window, then for input readiness. Each DLL load and each window/readiness wait uses the same Launch timeout from Settings > Advanced (default 20 seconds), even without mods. A timeout terminates the game and reports which step failed.
- The existing CLI timeout argument controls all these steps on Windows and in the Wine/Proton helper on Linux.
- A Steam account needs Steam running. On Windows, Steam's client runs the game. On Linux, the game runs under Steam's Proton, with Steam's client files and DXVK in its own prefix, `steam-prefix`.
- A non-Steam account on Linux runs under the system's Wine. If there's none, it runs under Steam's Proton, with its own prefix, `proton-prefix`.
- Launch default client starts the default install with no account.
- A failed launch shows the reason. The account's state returns to its normal state.

## Account states

- **Launching:** the launch is in progress.
- **Active:** the account's game is running and this launcher started it.
- **In Use:** the account's game is running, but this launcher didn't start it.
- **Inactive:** the game isn't running.

The game is matched by its exe path. The Linux and Windows builds use the same key.

## Issues

- **Blocking:**
  - Gw.exe is missing.
  - Steam isn't running for a Steam account.
  - Steam for Linux isn't installed for a Steam account. *(Linux only; no fix)*
  - No Proton is installed in Steam's library for a Steam account. *(Linux only)*
  - Wine isn't installed for a non-Steam account. *(Linux only)*
  - Texmods are active without gMod.dll.
  - A file is a Windows-compacted (WOF) file the launcher can't read. *(Linux only)*
- **Warning:** an active mod file is missing.
- **Fixes:**
  - Steam not running: starts Steam in the background.
  - No Proton: asks Steam to install Proton Experimental. Steam shows its own confirmation. *(Linux only)*
  - Wine missing: starts the distro's Wine install. Progress shows in Setup. *(Linux only)*
  - A WOF file can be decompressed. The compressed original is kept beside it. *(Linux only)*
  - A missing gMod.dll can be downloaded from gwdevhub/gMod.
  - An out-of-date Gw.exe can be updated.
- A fix runs from the issues window. The window's **Re-check** button runs the checks again, for problems fixed outside the launcher.
- After a fix starts something in the background, such as Steam or Wine, the launch waits for the next check. A launch rechecks the account, so it still stops if the problem remains.

## Account editor

- The title is required.
- Gw.exe must exist at the path. The email is optional.
- Mods can be added with the file picker, switched on and off, and removed.
- Mods from plugin folders are listed read-only when Use plugin folder mods is on.

**Linux note:** the editor has one Path field, and the path is used as it is. A drive-letter path that doesn't exist
as written falls back to the mount for that drive.

## Setup

- Setup appears when a required step is missing.
- The only step is Guild Wars client. The Gw.exe installs into the folder shown, and is recorded as the default install.
- Wine isn't a setup step. A non-Steam account on Linux needs Wine, and its issues say so (see Issues).
- One install runs at a time.

## Startup checks

- On Windows, before any window is made, the launcher checks for Microsoft's WebView2 runtime. Its window needs the runtime, so the check can't be a setup step.
- If the runtime is missing, a message offers to open Microsoft's installer page. The launcher then exits.
- Linux has no WebView2 check. It uses WebKitGTK.

## Settings

- Keep in system tray, Launch minimised, Prompt me when an update is available, and Launch timeout (seconds).
- The launch timeout uses whole seconds, from 1 to 4294967, and defaults to 20 seconds. Decimal input is rounded to the nearest whole second on save.
- Settings.json keeps the original TimeoutOnModlaunch field in milliseconds; existing saved durations are retained and now also control window readiness. The settings window displays them rounded to whole seconds, clamped to the input range, and saves them in that form.
- Update checks always run at startup. They aren't a setting.

## Updates

- At every startup the launcher checks for out-of-date Gw.exe files and for a newer release of itself.
- A newer release shows an update icon at the bottom right of the account list, with the title "Version r<revision> available". Clicking the icon opens the update prompt.
- With Prompt me when an update is available on, the update prompt also opens at startup when a release is found. Out-of-date Gw.exe files are offered the same way.
- With the setting off, no prompt opens at startup, but the icon still shows.
- Windows replaces the exe and restarts. Linux replaces the launcher, the helper and the loader from the release zip, then restarts.

## Master password

- The accounts file can be encrypted. Until the password is entered, the list is locked.
- Remember on this computer keeps the password for the next start.
- Windows keeps it protected by DPAPI in the user's registry. Linux keeps it in a file only the user can read.

## Shortcuts, clipboard and file picker

- A desktop shortcut for an account starts the launcher with `-launch <name>`, which launches that account at once.
  Windows writes a `.lnk`. Linux writes a `.desktop` file.
- Copy puts text on the clipboard.
- Browse opens the native file dialog, filtered to Gw.exe, or to mod files.

## Known differences, all in one place

| Behaviour | Windows | Linux | Reason |
| --- | --- | --- | --- |
| List hides on focus loss (tray mode) | Yes | Yes | Pointer position from GDK |
| Tray placement | Notification area at startup, click point on a click | Click point | Linux's tray doesn't give the rectangle |
| Context menu position | At pointer | Default position | Pointer position isn't readable |
| Tray has a menu | Yes (Open, Quit) | Yes (Open, Quit) | Both show the launcher's own menu |
| Tray detected | Checked | Checked | Taskbar's notification area on Windows; StatusNotifierItem watcher on Linux |
| WOF decompression | No | Yes | Windows reads its own compacted files |
| Wine needed | No | Non-Steam accounts only | Linux runs non-Steam games through Wine |
| Steam account runs under | Steam | Steam's Proton | Linux has no native Steam client for the game |
| WebView2 runtime check at startup | Yes | No | Windows window needs the runtime |
| Launcher update package | Exe | Zip | Platform release files |
| Remembered password storage | DPAPI | 0600 file | Platform secret storage |
| One instance at a time | Yes | Yes | Named pipe on Windows, socket in the data directory on Linux |
