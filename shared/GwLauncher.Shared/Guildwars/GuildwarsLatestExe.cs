using GW_Launcher.Guildwars.Models;
using GW_Launcher.Guildwars.Utils;

namespace GW_Launcher.Guildwars;

// Asks ArenaNet which Gw.exe build is current. Shared by the Windows and Linux launchers.
public static class GuildwarsLatestExe
{
    public static async Task<(FileResponse? Response, string Error)> GetAsync(CancellationToken cancellationToken = default)
    {
        var guildWarsClient = new GuildwarsClient();
        var result = await guildWarsClient.Connect(cancellationToken);
        if (!result.HasValue)
        {
            return (null, "Failed to connect to ArenaNet servers");
        }

        var (context, manifest) = result.Value;

        try
        {
            await guildWarsClient.Send(new FileRequest
            {
                Field1 = 0x3F2,
                Field2 = 0xC,
                FileId = manifest.LatestExe,
                Version = 0
            }, context, cancellationToken);

            var metadata = await guildWarsClient.ReceiveWait<FileMetadataResponse>(context, cancellationToken);
            if (metadata.Field1 == 0x4F2)
            {
                return (null, "Error 0x4F2: Could not find file");
            }
            else if (metadata.Field1 != 0x5F2)
            {
                return (null, $"Error: Unexpected field response; expected 0x5F2, got 0x{metadata.Field1:X}");
            }

            var response = await guildWarsClient.ReceiveWait<FileResponse>(context, cancellationToken);
            return (response, string.Empty);
        }
        finally
        {
            context.Socket?.Dispose();
        }
    }
}
