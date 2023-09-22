using Ionic.Zip;

namespace GW_Launcher.uMod;

public sealed class TpfEntry
{
    public string? Name { get; init; }
    public ZipEntry? Entry { get; init; }
    public uint CrcHash { get; init; }
}
