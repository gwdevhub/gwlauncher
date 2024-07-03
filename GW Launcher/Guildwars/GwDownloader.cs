using System;
using System.IO;
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
            // Implement the extraction logic here
            // This should decode the downloaded file and extract Gw.exe
            // Return the path to the extracted Gw.exe
            throw new NotImplementedException("Extraction logic needs to be implemented");
        }
    }
}
