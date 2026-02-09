using Microsoft.Win32;
using System.Reflection;

namespace MidiForwarder
{
    public class TrayManager : IDisposable
    {
        private NotifyIcon? notifyIcon;
        private ContextMenuStrip? trayMenu;
        private ToolStripMenuItem? minimizeToTrayItem;
        private ToolStripMenuItem? autoStartItem;
        private ToolStripMenuItem? aboutItem;
        private ToolStripMenuItem? exitItem;
        private ToolStripMenuItem? languageItem;
        private ToolStripMenuItem? langChineseItem;
        private ToolStripMenuItem? langEnglishItem;
        private ToolStripMenuItem? langSystemItem;

        public event EventHandler? ShowWindowRequested;
        public event EventHandler<bool>? AutoStartChanged;
        public event EventHandler<bool>? MinimizeToTrayChanged;
        public event EventHandler? ExitRequested;
        public event EventHandler<string>? LanguageChanged;

        private string currentLanguage = "";

        public static bool IsAutoStartEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                    if (key != null)
                    {
                        var value = key.GetValue("MidiForwarder");
                        return value != null;
                    }
                }
                catch { }
                return false;
            }
        }

        public TrayManager(bool minimizeToTray, string language)
        {
            currentLanguage = language;
            InitializeTrayIcon(minimizeToTray);
            LocalizationManager.LanguageChanged += (s, e) => UpdateLocalizedText();
        }

        private void InitializeTrayIcon(bool minimizeToTray)
        {
            trayMenu = new ContextMenuStrip();

            autoStartItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayAutoStart"), null, (s, e) => ToggleAutoStart())
            {
                Checked = IsAutoStartEnabled
            };
            minimizeToTrayItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayMinimizeToTray"), null, (s, e) => ToggleMinimizeToTray())
            {
                Checked = minimizeToTray
            };

            // 语言子菜单
            langChineseItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayLanguageChinese"), null, (s, e) => SetLanguage("zh-CN"));
            langEnglishItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayLanguageEnglish"), null, (s, e) => SetLanguage("en"));
            langSystemItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayLanguageSystem"), null, (s, e) => SetLanguage(""));

            languageItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayLanguage"));
            languageItem.DropDownItems.Add(langChineseItem);
            languageItem.DropDownItems.Add(langEnglishItem);
            languageItem.DropDownItems.Add(new ToolStripSeparator());
            languageItem.DropDownItems.Add(langSystemItem);

            UpdateLanguageMenuCheck();

            var separator1 = new ToolStripSeparator();
            aboutItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayAbout"), null, (s, e) => AboutDialog.Instance.Show());
            var separator2 = new ToolStripSeparator();
            exitItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayExit"), null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));

            trayMenu.Items.Add(autoStartItem);
            trayMenu.Items.Add(minimizeToTrayItem);
            trayMenu.Items.Add(languageItem);
            trayMenu.Items.Add(separator1);
            trayMenu.Items.Add(aboutItem);
            trayMenu.Items.Add(separator2);
            trayMenu.Items.Add(exitItem);

            notifyIcon = new NotifyIcon
            {
                Icon = LoadEmbeddedIcon(),
                Text = LocalizationManager.GetString("TrayTooltip"),
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            notifyIcon.DoubleClick += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateLocalizedText()
        {
            if (autoStartItem != null)
                autoStartItem.Text = LocalizationManager.GetString("TrayAutoStart");
            if (minimizeToTrayItem != null)
                minimizeToTrayItem.Text = LocalizationManager.GetString("TrayMinimizeToTray");
            if (languageItem != null)
                languageItem.Text = LocalizationManager.GetString("TrayLanguage");
            if (langChineseItem != null)
                langChineseItem.Text = LocalizationManager.GetString("TrayLanguageChinese");
            if (langEnglishItem != null)
                langEnglishItem.Text = LocalizationManager.GetString("TrayLanguageEnglish");
            if (langSystemItem != null)
                langSystemItem.Text = LocalizationManager.GetString("TrayLanguageSystem");
            if (aboutItem != null)
                aboutItem.Text = LocalizationManager.GetString("TrayAbout");
            if (exitItem != null)
                exitItem.Text = LocalizationManager.GetString("TrayExit");
            if (notifyIcon != null)
                notifyIcon.Text = LocalizationManager.GetString("TrayTooltip");
        }

        private void UpdateLanguageMenuCheck()
        {
            if (langChineseItem != null)
                langChineseItem.Checked = currentLanguage == "zh-CN";
            if (langEnglishItem != null)
                langEnglishItem.Checked = currentLanguage == "en";
            if (langSystemItem != null)
                langSystemItem.Checked = string.IsNullOrEmpty(currentLanguage);
        }

        private void SetLanguage(string language)
        {
            currentLanguage = language;
            UpdateLanguageMenuCheck();
            LanguageChanged?.Invoke(this, language);
        }

        public void UpdateLanguage(string language)
        {
            currentLanguage = language;
            UpdateLanguageMenuCheck();
        }

        public void UpdateMinimizeToTrayCheck(bool minimizeToTray)
        {
            if (minimizeToTrayItem != null)
            {
                minimizeToTrayItem.Checked = minimizeToTray;
            }
        }

        private void ToggleAutoStart()
        {
            bool currentState = IsAutoStartEnabled;
            SetAutoStart(!currentState);

            if (autoStartItem != null)
            {
                autoStartItem.Checked = !currentState;
            }

            AutoStartChanged?.Invoke(this, !currentState);
        }

        private void ToggleMinimizeToTray()
        {
            if (minimizeToTrayItem != null)
            {
                minimizeToTrayItem.Checked = !minimizeToTrayItem.Checked;
                MinimizeToTrayChanged?.Invoke(this, minimizeToTrayItem.Checked);
            }
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        var exePath = Application.ExecutablePath;
                        // 添加 --autostart 参数，用于区分开机自启动和手动启动
                        key.SetValue("MidiForwarder", $"\"{exePath}\" --autostart");
                    }
                    else
                    {
                        key.DeleteValue("MidiForwarder", false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置自启动失败: {ex.Message}");
            }
        }

        public void ShowBalloonTip(string title, string text)
        {
            notifyIcon?.ShowBalloonTip(2000, title, text, ToolTipIcon.Info);
        }

        public void Dispose()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        private static Icon LoadEmbeddedIcon()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "MidiForwarder.default_form_icon.ico";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    return new Icon(stream);
                }
            }
            catch { }

            // 如果加载失败，返回系统默认图标
            return SystemIcons.Application;
        }
    }
}
