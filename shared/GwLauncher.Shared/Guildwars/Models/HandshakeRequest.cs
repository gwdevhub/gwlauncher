namespace GW_Launcher.Guildwars.Models;
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct HandshakeRequest
{
    public byte Field1 { get; init; }
    public uint Field2 { get; init; }
    public ushort Field3 { get; init; }
    public ushort Field4 { get; init; }
    public uint Field5 { get; init; }
    public uint Field6 { get; init; }
    public uint Field7 { get; init; }
}
