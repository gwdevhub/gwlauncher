namespace GW_Launcher.uMod;

internal readonly struct TexmodMessage
{
    public readonly ControlMessage Message;
    public readonly uint Value;
    public readonly uint Hash;

    public TexmodMessage(ControlMessage message, uint value, uint hash)
    {
        Message = message;
        Value = value;
        Hash = hash;
    }
}
