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
    }
}
