using System;
using System.Net.Sockets;

namespace Daybreak.Services.Guildwars.Models;
internal readonly struct GuildwarsClientContext : IDisposable
{
    public Socket Socket { get; init; }

    public void Dispose()
    {
        this.Socket.Dispose();
    }
}
