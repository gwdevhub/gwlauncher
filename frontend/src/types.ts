// The shapes the host sends. Field names match the Rust side's JSON.

export interface Settings {
    checkForUpdates: boolean;
    promptForUpdates: boolean;
    keepInSystemTray: boolean;
    launchMinimized: boolean;
    showTitleBarInTray: boolean;
    listWidth: number;
    listHeight: number;
    timeoutOnModlaunch: number;
    steamPath: string;
    winePath: string;
    uiScale: number;
}

export interface UpdateInfo {
    revision: number;
}

export interface AppState {
    locked: boolean;
    startup: { view: string; title?: string; text?: string; issues?: unknown[] } | null;
    canCreateShortcuts: boolean;
    debug: boolean;
    canLaunchDefaultClient: boolean;
    hasLinuxPaths: boolean;
    detectedPaths?: { steam: string; wine: string };
    hasWindowsOptions: boolean;
    hasTray: boolean;
    hasSetup: boolean;
    setupNeeded: boolean;
    version?: string;
    update?: UpdateInfo | null;
    settings?: Settings;
}

// What the page draws around its view, from the host's chrome request.
export interface Chrome {
    title: string;
    titleBar: boolean;
    resizable: boolean;
}

export interface Role {
    view: string;
    args: any[];
}

export interface AccountEntry {
    name: string;
    icon: string | null;
    state: string;
    blocked: boolean;
    issues: Issue[];
}

export interface Issue {
    severity: number;
    message: string;
    wofPath?: string | null;
    canDownloadGMod?: boolean;
    canUpdateGwExe?: boolean;
    canInstallSteam?: boolean;
    canInstallProton?: boolean;
    canInstallWine?: boolean;
}

export interface ModDraft {
    fileName: string;
    type: number;
    active: boolean;
    linuxPath: string;
    target: string | null;
    readOnly: boolean;
    problem: string;
    canLocate: boolean;
    canResolve: boolean;
}

export interface AccountDraft {
    title: string;
    email: string;
    password: string;
    character: string;
    gwPath: string;
    linuxGwPath: string;
    extraArgs: string;
    usePluginFolderMods: boolean;
    steamAccount: boolean;
    elevated: boolean;
    mods: ModDraft[];
}

export interface MenuItem {
    text?: string;
    hidden?: boolean;
    separator?: boolean;
}

export interface Launch {
    issues?: Issue[];
    canLaunch?: boolean;
    error?: string | null;
}
