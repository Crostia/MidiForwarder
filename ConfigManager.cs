using System.Text.Json;

namespace MidiForwarder
{
    public class ConfigManager
    {
        private readonly string configFilePath;
        private AppConfig config = new();
        private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

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

        public void UpdateSelectedDevices(int inputId, int outputId)
        {
            config.SelectedInputDeviceId = inputId;
            config.SelectedOutputDeviceId = outputId;
            SaveConfig();
        }

        public void UpdateAutoConnect(bool autoConnect)
        {
            config.AutoConnectOnStartup = autoConnect;
            SaveConfig();
        }

        public void UpdateMinimizeToTray(bool minimizeToTray)
        {
            config.MinimizeToTray = minimizeToTray;
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
    }
}
