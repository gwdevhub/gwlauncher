namespace GwLauncher.Linux;

// Linux paths for accounts and mods whose Accounts.json paths are Windows paths.
// Keys are the Windows paths exactly as they appear in Accounts.json, so the Windows app's file stays untouched.
public sealed class PathSidecar
{
    [JsonProperty("accounts")] public Dictionary<string, AccountPaths> Accounts { get; set; } = new();

    public sealed class AccountPaths
    {
        [JsonProperty("linuxGwPath")] public string? LinuxGwPath { get; set; }

        [JsonProperty("mods")] public Dictionary<string, string> Mods { get; set; } = new();
    }

    // A missing file is empty. A file that doesn't parse throws rather than being treated as empty,
    // because saving over it would discard the user's Linux paths.
    public static PathSidecar Read(string path)
    {
        if (!File.Exists(path))
        {
            return new PathSidecar();
        }

        return JsonConvert.DeserializeObject<PathSidecar>(File.ReadAllText(path)) ?? new PathSidecar();
    }

    public void Write(string path)
    {
        File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
    }
}
