using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace GW_Launcher.Guildwars
{
    public static class GwDownloader
    {
        private const string GW_DOWNLOAD_URL = "https://guildwars.com/download/GwSetup.exe";

        public static async Task<string> DownloadGwExeAsync()
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(GW_DOWNLOAD_URL);
            response.EnsureSuccessStatusCode();

            var tempPath = Path.GetTempFileName();
            using (var fs = new FileStream(tempPath, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }

            return tempPath;
        }

        public static async Task<string> ExtractGwExeAsync(string setupFilePath)
        {
            string extractedPath = Path.Combine(Path.GetTempPath(), "Gw.exe");
            
            using (var archive = new ZipArchive(File.OpenRead(setupFilePath), ZipArchiveMode.Read))
            {
                var gwExeEntry = archive.GetEntry("Gw.exe");
                if (gwExeEntry == null)
                {
                    throw new FileNotFoundException("Gw.exe not found in the setup file.");
                }

                using (var entryStream = gwExeEntry.Open())
                using (var fileStream = File.Create(extractedPath))
                {
                    await entryStream.CopyToAsync(fileStream);
                }
            }

            return extractedPath;
        }
    }
}
