using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Media_Player.Scripts
{
    public static class UpdateManager
    {
        private static string? GITHUB_TOKEN = Environment.GetEnvironmentVariable("MP-UPDATER2-API-TOKEN");
        private static string? CURRENT_UPDATER_VERSION, CURRENT_APP_VERSION;
        private static string? LATEST_UPDATER_VER;
        private static bool IsClientInitialised = false;

        private static HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        public static void InitClient()
        {
            try
            {
                if (string.IsNullOrEmpty(GITHUB_TOKEN))
                {
                    throw new Exception("Github API Token is not set!");
                }
                if (!client.DefaultRequestHeaders.UserAgent.Any())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:146.0) Gecko/20100101 Firefox/146.0");
                }
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("token", GITHUB_TOKEN);

                IsClientInitialised = true;
                CURRENT_APP_VERSION = GetCurrentAppVersion();
                CURRENT_UPDATER_VERSION = GetCurrentUpdaterVersion();
                Debug.WriteLine(CURRENT_UPDATER_VERSION);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public static async Task<string> GetLatestVersion()
        {
            if (!IsClientInitialised) InitClient();
            var response = await client.GetStringAsync(
                              "https://api.github.com/repos/alexandrurahaian/Media-Player-Updater/releases/latest"
                          );
            var json = JsonNode.Parse(response);
            var tag = json?["tag_name"]?.ToString();
            return string.IsNullOrWhiteSpace(tag)
                    ? CURRENT_APP_VERSION
                    : tag.TrimStart('v');
        }

        private static bool IsVersionNewer(string? latest, string? current)
        {
            if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(current)) return false;

            var latestParts = latest.Split('.').Select(int.Parse).ToArray();
            var currentParts = current.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
            {
                int l = i < latestParts.Length ? latestParts[i] : 0;
                int c = i < currentParts.Length ? currentParts[i] : 0;

                if (l > c) return true;
                if (l < c) return false;
            }

            return false;
        }


        public static async Task<bool?> IsLatestUpdater()
        {
            return IsVersionNewer(LATEST_UPDATER_VER, CURRENT_UPDATER_VERSION);
        }

        public static async Task CheckForUpdates()
        {
            string? latest_str = await GetLatestVersion();
            LATEST_UPDATER_VER = latest_str;
            if (IsVersionNewer(LATEST_UPDATER_VER, CURRENT_UPDATER_VERSION))
                await DownloadUpdate();
        }
        private static async Task DownloadUpdate()
        {
            const string download_url = "https://github.com/alexandrurahaian/Media-Player-Updater/releases/latest/download/updater.exe";
            string updater_path = Path.Combine(AppContext.BaseDirectory, "updater.exe");
            try
            {
                if (!IsClientInitialised) InitClient();

                if (File.Exists(updater_path)) File.Delete(updater_path); // deletes the old updater exe

                using (var s = await client.GetStreamAsync(new Uri(download_url)))
                {
                    using (var fs = new FileStream(updater_path, FileMode.CreateNew))
                    {
                        await s.CopyToAsync(fs);
                    }
                }

                CURRENT_UPDATER_VERSION = GetCurrentUpdaterVersion(); // updates version
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to install newer version of updater.exe: {ex.Message}");
            }
        }

        public static string? GetCurrentAppVersion()
        {
            return FileVersionInfo.GetVersionInfo(Path.Combine(AppContext.BaseDirectory, "Media Player.exe")).FileVersion;
        }
        public static string? GetCurrentUpdaterVersion()
        {
            return FileVersionInfo.GetVersionInfo(Path.Combine(AppContext.BaseDirectory, "updater.exe")).FileVersion;
        }
        
    }
}
