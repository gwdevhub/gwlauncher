namespace GW_Launcher.Guildwars.Models;
public readonly struct FileResponse
{
    public int FileId { get; init; }
    public int SizeDecompressed { get; init; }
    public int SizeCompressed { get; init; }
    public int Crc { get; init; }
}
