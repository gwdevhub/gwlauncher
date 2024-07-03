using System.Runtime.InteropServices;

namespace GW_Launcher.Guildwars.Models;
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct ManifestResponse
{
    public readonly short Field1;
    public readonly short Field2;
    public readonly int Field3;
    public readonly int Manifest;
    public readonly int BackupExe;
    public readonly int Field6;
    public readonly int Field7;
    public readonly int Field8;
    public readonly int LatestExe;
}
