namespace MidiForwarder
{
    public class AppConfig
    {
        public int SelectedInputDeviceId { get; set; } = -1;
        public int SelectedOutputDeviceId { get; set; } = -1;
        public bool AutoBoot { get; set; } = false;  // 开机自启动
        public bool AutoConnectOnStartup { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;  // 关闭时最小化到托盘
        public bool HasShownTrayPrompt { get; set; } = false;
        public string Language { get; set; } = ""; // 空字符串表示使用系统默认语言
        public bool AutoCheckUpdate { get; set; } = true; // 启动时自动检查更新
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue; // 上次检查更新时间
        public string? IgnoredVersion { get; set; } = null; // 被忽略的版本号

        // 开机启动延迟（分钟），默认2分钟
        public int AutoBootDelayMinutes { get; set; } = 2;

        // 自动连接重试间隔（秒），默认30秒
        public int AutoConnectRetryIntervalSeconds { get; set; } = 30;

        // 内部计算属性：开机启动时是否最小化
        // 只有当 AutoBoot 和 MinimizeToTrayOnClose 都为 true 时才最小化
        public bool StartMinimizedOnAutoStart => AutoBoot && MinimizeToTrayOnClose;
    }
}
