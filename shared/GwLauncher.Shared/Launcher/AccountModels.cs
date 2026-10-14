using GW_Launcher.Classes;

namespace GW_Launcher.Launcher;

public enum IssueSeverity
{
    // Stops the account from launching.
    Blocking,

    // Doesn't stop the launch, but something won't load.
    Warning
}

// The optional flags name the fix a window can offer: decompress a Wof file, download gMod.dll, or update Gw.exe.
public sealed record AccountIssue(IssueSeverity Severity, string Message, string? WofPath = null, bool CanDownloadGMod = false, bool CanUpdateGwExe = false);

public sealed record AccountEntry(string Name, IReadOnlyList<AccountIssue> Issues, string State = "Inactive")
{
    public bool Blocked => Issues.Any(issue => issue.Severity == IssueSeverity.Blocking);
}

// An account's fields as the editor shows them. Each host maps it to and from its own account format.
public sealed class AccountDraft
{
    public string Title { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Character { get; set; } = "";
    public string GwPath { get; set; } = "";
    public string LinuxGwPath { get; set; } = "";
    public string ExtraArgs { get; set; } = "";
    public bool UsePluginFolderMods { get; set; } = true;

    // Windows only: a Steam account starts through Steam, and an elevated one runs as administrator.
    public bool SteamAccount { get; set; }
    public bool Elevated { get; set; }
    public List<ModDraft> Mods { get; set; } = new();
}

public sealed class ModDraft
{
    public string FileName { get; set; } = "";
    public ModType Type { get; set; }
    public bool Active { get; set; } = true;

    // The path on this computer when it isn't FileName (Linux only).
    public string LinuxPath { get; set; } = "";

    // Plugin-folder files: the shortcut's target, and whether the list may change the file.
    public string? Target { get; set; }
    public bool ReadOnly { get; set; }

    // Set by the host: why the mod can't load here, whether its path can be chosen, and whether it's a compressed file.
    public string Problem { get; set; } = "";
    public bool CanLocate { get; set; }
    public bool CanResolve { get; set; }

    public ModDraft Clone() => (ModDraft)MemberwiseClone();
}
