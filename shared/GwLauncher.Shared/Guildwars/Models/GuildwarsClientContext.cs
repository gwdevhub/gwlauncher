using System;
using System.Net.Sockets;

namespace GW_Launcher.Guildwars.Models;
internal readonly struct GuildwarsClientContext : IDisposable
{
    public Socket Socket { get; init; }

    public void Dispose()
    {
        this.Socket.Dispose();
    }
}
