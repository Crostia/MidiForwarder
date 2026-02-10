namespace MidiForwarder
{
    public class AppConfig
    {
        public int SelectedInputDeviceId { get; set; } = -1;
        public int SelectedOutputDeviceId { get; set; } = -1;
        public bool AutoStart { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool AutoConnectOnStartup { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool HasShownTrayPrompt { get; set; } = false;
        public string Language { get; set; } = ""; // 空字符串表示使用系统默认语言
        public bool AutoCheckUpdate { get; set; } = true; // 启动时自动检查更新
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue; // 上次检查更新时间
    }
}
