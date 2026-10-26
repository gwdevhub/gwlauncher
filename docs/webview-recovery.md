# Interface startup and recovery

Tracking issue: https://github.com/gwdevhub/gwlauncher/issues/264

## Failure contract

- The Windows window stays hidden until the frontend has mounted its first view and acknowledged readiness.
- A 30-second native startup watchdog covers environment/controller creation, navigation, and frontend initialization. Startup/read bridge requests have a 15-second deadline; launch, update, and interactive requests do not inherit that deadline.
- A separate Windows watchdog runs independently of the UI pump during loader/configuration startup. If initial synchronous settings/account-file I/O stalls for 30 seconds, it shows the blocked startup stage in a native diagnostic and offers acknowledgment followed by process exit. Time spent reading an existing startup error/runtime-installation dialog does not count. It does not attempt to retry an operation still blocked in the original thread.
- Native creation, navigation, messaging, script, and process failures open a Windows Retry/Cancel dialog. Ctrl+C copies its full diagnostic text. Retry creates a fresh WebView, invalidates its old callbacks/replies, and restores the latest progress snapshot. Cancel exits the launcher. Running jobs are not restarted by Retry.
- Uncaught frontend errors use that native path too. The frontend also has an initialization-error screen. If JavaScript cannot execute at all, the native watchdog remains responsible for reporting failure.
- Account loading has distinct loading, failure, and successfully empty states. A failed refresh retains existing rows; a later successful refresh can change the account count.
- Account-file reloads run on workers. Mutations are blocked during reload and after an account-file read failure. Retry rereads the file without discarding the last successfully loaded rows on failure.
- Process scans run on workers. Rows use cached status immediately. One scan runs at a time; a scan exceeding 15 seconds shows a stale-status warning.
- The single account list uses one namespace-private cache and reload gate, rather than per-window cache ownership in `AppState`. Editor requests reuse the same account-list data. The cache survives hiding/recreating the list's WebView.
- JavaScript requests file icons through `pathIcon(path)`, independently of account loading. The backend resolves native/Wine paths on workers and returns PNG bytes as a JSON array of numbers, or null for missing/unsupported files. PE, ICO and PNG inputs use the existing image helpers.
- The PNG cache is process-owned and survives page/window recreation. Canonical paths share entries; requests for the same file serialize extraction, and modification timestamps invalidate changed files. Icon loading no longer occurs in account replies. A separate worker pool keeps optional icon I/O from occupying game-launch workers. Frontend `PathIcon` components deduplicate in-flight requests and revoke their blob URLs when replaced/unmounted. Explicit account refresh and Update All request icons again.
- Unexpected worker/COM callback exceptions and UI dispatch failures produce native errors. Fatal internal errors require restarting rather than leaving an incomplete operation silently pending.

## Windows verification matrix

Run these against a separately requested Windows build. Fault injection requires a debugger or a local instrumented build; do not introduce faults into a user's live account file. Use a disposable copy of the configuration.

| Fault / action | Expected result |
| --- | --- |
| Block loader initialization or initial settings/account-file reading before the UI pump starts | Independent watchdog displays the startup stage after 30 seconds; acknowledging the native diagnostic exits the blocked launcher |
| Reject the first `accounts` reply; allow the next poll to succeed | Visible error followed by populated rows, regardless of account count |
| Delay `accounts` past 15 seconds or drop its reply | Read timeout and Retry, never the successful-empty UI |
| Add/remove accounts while the list is open | Next successful refresh reflects the new count |
| Leave the view while a read is pending | Its eventual reply does not update the new view |
| Corrupt/unreadable Accounts.json at startup, then repair and Retry | Error explains the read failure; saving is blocked until reread succeeds |
| Fail an explicit reload after a successful load | Previously displayed rows remain; file mutations are blocked; Retry can recover |
| Delay a reload indefinitely | Native UI remains responsive; request timeout/reload warning is visible; no second reload or account save runs concurrently |
| Slow/unavailable executable path, multiple distinct executables, changed executable timestamp | Names and fallback icons render immediately; icon requests cannot hold up account replies; explicit refresh picks up changed icons |
| Request 32-/64-bit PE, ICO and PNG icons through `pathIcon`, including native/Wine paths with spaces and Unicode | Replies contain PNG byte arrays; missing/unsupported files return null and the component keeps its fallback |
| Request the same canonical file concurrently, then destroy/recreate its WebView and request again | Backend extraction is shared and cached across page lifetimes; stale replies cannot update a replaced page; old blob URLs are released |
| Return an immediate failing HRESULT from environment/controller creation or navigation | Native error identifies the operation and HRESULT; Retry uses a new instance |
| Return failed navigation completion | Native error includes WebView2 web error status; failed page is not marked ready |
| Drop a startup reply, break the frontend before mounting, or prevent the ready handshake | Frontend error or native watchdog gives an actionable failure within the configured deadline |
| Terminate/hang the renderer after startup | Native recovery dialog; Retry recreates the interface |
| Throw inside a host-delivered JavaScript event or fail ExecuteScript | Native message-delivery error instead of a silently lost reply |
| Unload/destroy a window while creation or a long-running reply is outstanding | Old callbacks do not access its native state or deliver to a replacement page |
| Fail/retry a progress window while its job pauses, finishes, or fails | Recreated page restores current controls/result without restarting the job |
| Complete background work while a native dialog is open | UI tasks still dispatch through the message-only window |
| Fail UI task posting or throw an uncaught worker/COM callback exception | Visible fatal diagnostic, followed by exit/restart guidance |
| Run a launch/update longer than 15 seconds | No short read-request timeout is imposed on the operation |
| Repeat open/hide/cooldown/reopen and native Retry cycles | No accumulation of caller-owned COM handler references or stale callbacks |

Also verify ordinary startup, encrypted-file unlock, tray mode, quick launch, account editing, reordering, and Linux window startup. Static type checks do not substitute for these platform checks.
