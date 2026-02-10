using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MidiForwarder
{
    public class UpdateInfo
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("body")]
        public string Body { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";

        [JsonPropertyName("published_at")]
        public string PublishedAt { get; set; } = "";

        [JsonPropertyName("assets")]
        public List<ReleaseAsset> Assets { get; set; } = new();
    }

    public class ReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    public class UpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string ReleaseUrl { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public bool IsError { get; set; }
    }

    public class UpdateManager : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly string owner = "Crostia";
        private readonly string repo = "MidiForwarder";
        private readonly string currentVersion;

        public UpdateManager()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "MidiForwarder-UpdateChecker");
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            // 获取当前程序版本
            currentVersion = GetCurrentVersion();
        }

        private string GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString(3) ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            var result = new UpdateCheckResult
            {
                CurrentVersion = currentVersion
            };

            try
            {
                var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    result.IsError = true;
                    result.ErrorMessage = $"Failed to check for updates. Status: {response.StatusCode}";
                    return result;
                }

                var json = await response.Content.ReadAsStringAsync();
                var releaseInfo = JsonSerializer.Deserialize<UpdateInfo>(json);

                if (releaseInfo == null)
                {
                    result.IsError = true;
                    result.ErrorMessage = "Failed to parse release information.";
                    return result;
                }

                var latestVersion = releaseInfo.TagName.TrimStart('v', 'V');
                result.LatestVersion = latestVersion;
                result.ReleaseNotes = releaseInfo.Body;
                result.ReleaseUrl = releaseInfo.HtmlUrl;

                // 查找安装包资源
                var installerAsset = releaseInfo.Assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.Contains("Installer", StringComparison.OrdinalIgnoreCase));

                if (installerAsset != null)
                {
                    result.DownloadUrl = installerAsset.BrowserDownloadUrl;
                }
                else
                {
                    // 如果没有找到安装包，使用发布页面URL
                    result.DownloadUrl = releaseInfo.HtmlUrl;
                }

                // 比较版本号
                result.HasUpdate = IsNewerVersion(latestVersion, currentVersion);

                return result;
            }
            catch (TaskCanceledException)
            {
                result.IsError = true;
                result.ErrorMessage = "Connection timed out. Please check your internet connection.";
                return result;
            }
            catch (Exception ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"Error checking for updates: {ex.Message}";
                return result;
            }
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var latestVersion = Version.Parse(latest);
                var currentVersion = Version.Parse(current);
                return latestVersion > currentVersion;
            }
            catch
            {
                // 如果解析失败，进行字符串比较
                return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
            }
        }

        public void OpenReleasePage(string url)
        {
            try
            {
                if (!string.IsNullOrEmpty(url))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open browser: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}
