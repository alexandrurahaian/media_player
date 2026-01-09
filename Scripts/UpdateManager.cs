using Media_Player.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
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

    public static class ConfigManager
    {
        public static DateTime LastCheckUtc
        {
            get => Settings.Default.LastCheckedUtc;
            set
            {
                Settings.Default.LastCheckedUtc = value;
                Settings.Default.Save();
            }
        }

        public static int CheckedAmount
        {
            get => Settings.Default.CheckedAmount;
            set
            {
                Settings.Default.CheckedAmount = value;
                Settings.Default.Save();
            }
        }
    }

    public static class UpdateManager
    {
        public static string? CURRENT_UPDATER_VERSION, CURRENT_APP_VERSION;
        private static string? LATEST_UPDATER_VER;
        private static bool IsClientInitialised = false;
        private const int MIN_HOURS_BETWEEN_UPDATES = 1;

        private static HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        public static void InitClient()
        {
            try
            {
                if (!client.DefaultRequestHeaders.UserAgent.Any())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:146.0) Gecko/20100101 Firefox/146.0");
                }

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
            if ((DateTime.UtcNow - ConfigManager.LastCheckUtc).TotalHours < MIN_HOURS_BETWEEN_UPDATES) return string.Empty;

            // return "1.2.0"; // debug purposes
            if (!IsClientInitialised) InitClient();
            var response = await client.GetStringAsync(
                              "https://api.github.com/repos/alexandrurahaian/Media-Player-Updater/releases/latest"
                          );
            var json = JsonNode.Parse(response);
            var tag = json?["tag_name"]?.ToString();

            ConfigManager.LastCheckUtc = DateTime.UtcNow;
            ConfigManager.CheckedAmount++;

            return string.IsNullOrWhiteSpace(tag)
                    ? CURRENT_APP_VERSION
                    : tag.TrimStart('v');
        }

        public static async Task<string> GetLatestAppVersion()
        {
            if ((DateTime.UtcNow - ConfigManager.LastCheckUtc).TotalHours < MIN_HOURS_BETWEEN_UPDATES) return string.Empty;
            // return "1.5.1"; // debug purposes
            if (!IsClientInitialised) InitClient();
            var response = await client.GetStringAsync(
                              "https://api.github.com/repos/alexandrurahaian/media_player/releases/latest"
                          );
            var json = JsonNode.Parse(response);
            var tag = json?["tag_name"]?.ToString();

            ConfigManager.LastCheckUtc = DateTime.UtcNow;
            ConfigManager.CheckedAmount++;

            return string.IsNullOrWhiteSpace(tag)
                    ? CURRENT_APP_VERSION
                    : tag.TrimStart('v');
        }

        private static bool IsVersionNewer(string? latest, string? current)
        {
            Debug.WriteLine($"Latest: {latest}");
            Debug.WriteLine($"Current: {current}");
            if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(current)) return false;

            var l = latest.Split('.');
            var c = current.Split('.');
            int max = Math.Max(l.Length, c.Length);

            for (int i = 0; i < max; i++)
            {
                int lv = i < l.Length ? int.Parse(l[i]) : 0;
                int cv = i < c.Length ? int.Parse(c[i]) : 0;

                if (lv > cv) return true;
                if (lv < cv) return false;
            }

            return false;
        }


        public static async Task<bool?> IsLatestUpdater()
        {
            return IsVersionNewer(await GetLatestVersion(), GetCurrentUpdaterVersion());
        }

        public static async Task<bool?> IsLatestApp()
        {
            return IsVersionNewer(await GetLatestAppVersion(), CURRENT_APP_VERSION);
        }

        public static async Task CheckForUpdates(bool updater_only = false)
        {
            string? latest_str = await GetLatestVersion();
            string? latest_app = await GetLatestAppVersion();
            LATEST_UPDATER_VER = latest_str;
            
            if (IsVersionNewer(LATEST_UPDATER_VER, CURRENT_UPDATER_VERSION))
                await DownloadUpdate();
            
            if (!updater_only && IsVersionNewer(latest_app, CURRENT_APP_VERSION))
                StartUpdater(); 
        }

        public static void StartUpdater()
        {
            Process updaterProc = new Process();
            updaterProc.StartInfo.FileName = Path.Combine(AppContext.BaseDirectory, "updater.exe");
            updaterProc.Start();
            Application.Current.Shutdown(0);
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
