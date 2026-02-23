namespace MidiForwarder
{
    public class AppConfig
    {
        // 设备信息格式: "[ID] 设备名称"，例如 "[0] MIDI Keyboard"
        public string SelectedInputDevice { get; set; } = "";
        public string SelectedOutputDevice { get; set; } = "";
        public bool AutoBoot { get; set; } = false;  // 开机自启动
        public bool AutoConnectOnStartup { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;  // 关闭时最小化到托盘
        public bool HasShownTrayPrompt { get; set; } = false;
        public string Language { get; set; } = ""; // 空字符串表示使用系统默认语言
        public bool AutoCheckUpdate { get; set; } = true; // 启动时自动检查更新
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue; // 上次检查更新时间
        public string? IgnoredVersion { get; set; } = null; // 被忽略的版本号

        // 自动连接重试间隔（秒），默认30秒
        public int AutoConnectRetryIntervalSeconds { get; set; } = 30;

        // 有线设备排除名单（用于过滤误识别的蓝牙设备）
        public List<string> WiredDeviceExclusions { get; set; } = [];
    }
}
