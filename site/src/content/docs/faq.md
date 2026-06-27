---
title: FAQ
description: Quick answers to common GW Launcher questions.
section: getting-started
---

### What is GW Launcher?

A lightweight Windows launcher for Guild Wars that stores your accounts, logs them in with a
double-click, lets you run several at once, and auto-loads DLL plugins and texture mods. It's
made by the team behind [GWToolbox++](https://www.gwtoolbox.com/).

### Is it safe? My antivirus complains.

Yes. It's open source and has been used since 2010. Antivirus warnings are a false positive
caused by mod injection — see [Troubleshooting](/docs/troubleshooting/).

If your **browser** blocks the download itself (Chrome, Edge, Brave, etc. flagging
`GW_Launcher.exe`), that's the same false positive — lower your browser's Safe Browsing level
before re-downloading. See
[Your browser blocked the download](/docs/troubleshooting/#your-browser-blocked-the-download).

### Is it free?

Free and open source (MIT licensed). Windows only.

### Which download do I pick?

The **self-contained** `GW_Launcher.exe` — it runs anywhere with no extra installs. The
framework-dependent build is smaller but needs the .NET 8 Desktop Runtime. See
[Install](/docs/installation/).

### Why does it need its own folder?

It writes `Settings.json`, `Accounts.json`, and `d3d9.dll` next to itself. Keep it in a dedicated
folder so those don't clutter Downloads or your Desktop. See [Files & Storage](/docs/files/).

### Can I run multiple accounts at the same time?

Yes — each needs its own copy of the game. See [Run Multiple Accounts](/docs/multiboxing/).

### Do I still need uMod for texture mods?

No. GW Launcher loads `.tpf` / `.zip` TexMods natively via gMod. See [TexMods](/docs/texmods/).

### How do I add GWToolbox?

Put `GWToolbox.dll` in a `plugins` folder next to `GW_Launcher.exe`, or add it to an account in
the Mod Manager. See [DLL Plugins](/docs/plugins/).

### I bought Guild Wars on Steam.

Tick **Steam Account** when adding the account and make sure Steam is running before you launch.
See [Steam Accounts](/docs/steam/).

### Are my passwords safe?

You can encrypt `Accounts.json` behind a master password. See
[Encryption & Passwords](/docs/encryption/).

### Is there a Linux version?

GW Launcher is Windows only. Many players run it under Wine/Proton, but it isn't officially
supported.

### How do I launch one account directly from my desktop?

Right-click the account and pick **Create Desktop Shortcut**. See [Tray & Shortcuts](/docs/tray/).
