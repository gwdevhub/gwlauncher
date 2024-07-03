namespace GW_Launcher.Guildwars.Models;
internal readonly struct FileResponse
{
    public int FileId { get; init; }
    public int SizeDecompressed { get; init; }
    public int SizeCompressed { get; init; }
    public int Crc { get; init; }
}
