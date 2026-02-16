using System.Text.Json;

namespace MidiForwarder
{
    public partial class ConfigManager
    {
        private readonly string configFilePath;
        private AppConfig config = new();
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public AppConfig Config => config ?? new AppConfig();

        public ConfigManager()
        {
            configFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MidiForwarder",
                "config.json"
            );
            LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    var json = File.ReadAllText(configFilePath);
                    config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                else
                {
                    config = new AppConfig();
                }
            }
            catch
            {
                config = new AppConfig();
            }
        }

        public void SaveConfig()
        {
            try
            {
                var directory = Path.GetDirectoryName(configFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(config, jsonOptions);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                // 配置保存失败时不抛出异常
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        public void UpdateSelectedDevices(string inputDevice, string outputDevice)
        {
            config.SelectedInputDevice = inputDevice;
            config.SelectedOutputDevice = outputDevice;
            SaveConfig();
        }

        // 从设备信息字符串中解析ID，格式: "[ID] 设备名称"
        public static int? ParseDeviceId(string deviceInfo)
        {
            if (string.IsNullOrEmpty(deviceInfo)) return null;

            try
            {
                var match = MyRegex().Match(deviceInfo);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int id))
                {
                    return id;
                }
            }
            catch { }

            return null;
        }

        // 从设备信息字符串中获取设备名称（不含ID）
        public static string ParseDeviceName(string deviceInfo)
        {
            if (string.IsNullOrEmpty(deviceInfo)) return "";

            try
            {
                var match = MyRegex1().Match(deviceInfo);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch { }

            return deviceInfo;
        }

        public void UpdateAutoConnect(bool autoConnect)
        {
            config.AutoConnectOnStartup = autoConnect;
            SaveConfig();
        }

        public void UpdateMinimizeToTrayOnClose(bool minimizeToTrayOnClose)
        {
            config.MinimizeToTrayOnClose = minimizeToTrayOnClose;
            SaveConfig();
        }

        public void SetTrayPromptShown()
        {
            config.HasShownTrayPrompt = true;
            SaveConfig();
        }

        public void UpdateLanguage(string language)
        {
            config.Language = language;
            SaveConfig();
        }

        public void UpdateAutoCheckUpdate(bool autoCheck)
        {
            config.AutoCheckUpdate = autoCheck;
            SaveConfig();
        }

        public void UpdateLastUpdateCheck(DateTime checkTime)
        {
            config.LastUpdateCheck = checkTime;
            SaveConfig();
        }

        public void UpdateIgnoredVersion(string? version)
        {
            config.IgnoredVersion = version;
            SaveConfig();
        }

        /// <summary>
        /// 添加设备到有线设备排除名单（防重复）
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns>是否成功添加（false表示已存在）</returns>
        public bool AddToWiredDeviceExclusions(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) return false;

            // 获取纯设备名称（不含ID）
            var pureName = ParseDeviceName(deviceName);
            if (string.IsNullOrWhiteSpace(pureName)) return false;

            // 检查是否已存在（不区分大小写）
            if (config.WiredDeviceExclusions.Any(e =>
                string.Equals(e, pureName, StringComparison.OrdinalIgnoreCase)))
            {
                return false; // 已存在
            }

            config.WiredDeviceExclusions.Add(pureName);
            SaveConfig();
            return true;
        }

        /// <summary>
        /// 从有线设备排除名单中移除设备
        /// </summary>
        public bool RemoveFromWiredDeviceExclusions(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) return false;

            var pureName = ParseDeviceName(deviceName);
            var existing = config.WiredDeviceExclusions.FirstOrDefault(e =>
                string.Equals(e, pureName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                config.WiredDeviceExclusions.Remove(existing);
                SaveConfig();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取有线设备排除名单
        /// </summary>
        public List<string> GetWiredDeviceExclusions()
        {
            return [.. config.WiredDeviceExclusions];
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"^\[(\d+)\]")]
        private static partial System.Text.RegularExpressions.Regex MyRegex();
        [System.Text.RegularExpressions.GeneratedRegex(@"^\[\d+\]\s*(.+)$")]
        private static partial System.Text.RegularExpressions.Regex MyRegex1();
    }
}
