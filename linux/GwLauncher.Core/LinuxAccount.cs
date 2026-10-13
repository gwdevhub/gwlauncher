namespace GwLauncher.Linux;

// Property names match the WinForms Account JSON so an Accounts.json round-trips between platforms.
public sealed class LinuxAccount
{
    [JsonProperty("email")] public string Email { get; set; } = "";

    [JsonProperty("password")] public string Password { get; set; } = "";

    [JsonProperty("character")] public string Character { get; set; } = "";

    [JsonProperty("gwpath")] public string GwPath { get; set; } = "";

    [JsonProperty("extraargs")] public string ExtraArgs { get; set; } = "";

    [JsonProperty("title")] public string Title { get; set; } = "";

    [JsonProperty("mods")] public List<LinuxMod> Mods { get; set; } = new();

    // Whether the plugins folders apply to this account; the Windows launcher reads the same flag.
    [JsonProperty("usePluginFolderMods")] public bool UsePluginFolderMods { get; set; } = true;

    // Kept in the sidecar file (see PathSidecar), never in Accounts.json.
    [JsonIgnore] public string? LinuxGwPath { get; set; }

    [JsonIgnore] public string EffectiveGwPath => string.IsNullOrEmpty(LinuxGwPath) ? GwPath : LinuxGwPath;

    // Fields this build doesn't use (elevated, is_steam_account, ...) are kept verbatim so saving never drops them.
    [JsonExtensionData] public IDictionary<string, JToken> Extra { get; set; } = new Dictionary<string, JToken>();

    [JsonIgnore]
    public string Name =>
        !string.IsNullOrEmpty(Title) ? Title :
        !string.IsNullOrEmpty(Character) ? Character :
        Email;
}
