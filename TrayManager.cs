using Microsoft.Win32;
using System.Reflection;

namespace MidiForwarder
{
    public class TrayManager : IDisposable
    {
        private NotifyIcon? notifyIcon;
        private ContextMenuStrip? trayMenu;
        private ToolStripMenuItem? minimizeToTrayItem;
        private ToolStripMenuItem? autoBootItem;
        private ToolStripMenuItem? aboutItem;
        private ToolStripMenuItem? exitItem;
        private ToolStripMenuItem? languageItem;
        private ToolStripMenuItem? langChineseItem;
        private ToolStripMenuItem? langEnglishItem;
        private ToolStripMenuItem? langSystemItem;
        private ToolStripMenuItem? checkUpdateItem;
        private ToolStripMenuItem? autoCheckUpdateItem;

        public event EventHandler? ShowWindowRequested;
        public event EventHandler<bool>? AutoBootChanged;
        public event EventHandler<bool>? MinimizeToTrayOnCloseChanged;

        public event EventHandler? ExitRequested;
        public event EventHandler<string>? LanguageChanged;
        public event EventHandler? CheckUpdateRequested;
        public event EventHandler<bool>? AutoCheckUpdateChanged;

        private string currentLanguage = "";

        public static bool IsAutoBootEnabled
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

        public TrayManager(bool minimizeToTrayOnClose, string language, bool autoCheckUpdate = true)
        {
            currentLanguage = language;
            InitializeTrayIcon(minimizeToTrayOnClose, autoCheckUpdate);
            LocalizationManager.LanguageChanged += (s, e) => UpdateLocalizedText();
        }

        private void InitializeTrayIcon(bool minimizeToTrayOnClose, bool autoCheckUpdate)
        {
            trayMenu = new ContextMenuStrip();

            autoBootItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayAutoBoot"), null, (s, e) => ToggleAutoBoot())
            {
                Checked = IsAutoBootEnabled
            };
            minimizeToTrayItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayMinimizeToTrayOnClose"), null, (s, e) => ToggleMinimizeToTrayOnClose())
            {
                Checked = minimizeToTrayOnClose
            };

            // 语言子菜单
            langChineseItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayLanguageChinese"), null, (s, e) => SetLanguage("zh-CN"));
            langEnglishItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayLanguageEnglish"), null, (s, e) => SetLanguage("en-US"));
            langSystemItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayLanguageSystem"), null, (s, e) => SetLanguage(""));

            languageItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayLanguage"));
            languageItem.DropDownItems.Add(langChineseItem);
            languageItem.DropDownItems.Add(langEnglishItem);
            languageItem.DropDownItems.Add(new ToolStripSeparator());
            languageItem.DropDownItems.Add(langSystemItem);

            UpdateLanguageMenuCheck();

            // 更新相关菜单项
            autoCheckUpdateItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayAutoCheckUpdate"), null, (s, e) => ToggleAutoCheckUpdate())
            {
                Checked = autoCheckUpdate
            };
            checkUpdateItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayCheckUpdate"), null, (s, e) => CheckUpdateRequested?.Invoke(this, EventArgs.Empty));

            var separator1 = new ToolStripSeparator();
            aboutItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayAbout"), null, (s, e) => AboutDialog.Instance.Show());
            var separator2 = new ToolStripSeparator();
            exitItem = new ToolStripMenuItem(LocalizationManager.GetString("TrayExit"), null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));

            trayMenu.Items.Add(autoBootItem);
            trayMenu.Items.Add(minimizeToTrayItem);
            trayMenu.Items.Add(languageItem);
            trayMenu.Items.Add(autoCheckUpdateItem);
            trayMenu.Items.Add(checkUpdateItem);
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
            if (autoBootItem != null)
                autoBootItem.Text = LocalizationManager.GetString("TrayAutoBoot");
            if (minimizeToTrayItem != null)
                minimizeToTrayItem.Text = LocalizationManager.GetString("TrayMinimizeToTrayOnClose");
            if (languageItem != null)
                languageItem.Text = LocalizationManager.GetString("TrayLanguage");
            if (langChineseItem != null)
                langChineseItem.Text = LocalizationManager.GetString("TrayLanguageChinese");
            if (langEnglishItem != null)
                langEnglishItem.Text = LocalizationManager.GetString("TrayLanguageEnglish");
            if (langSystemItem != null)
                langSystemItem.Text = LocalizationManager.GetString("TrayLanguageSystem");
            if (autoCheckUpdateItem != null)
                autoCheckUpdateItem.Text = LocalizationManager.GetString("TrayAutoCheckUpdate");
            if (checkUpdateItem != null)
                checkUpdateItem.Text = LocalizationManager.GetString("TrayCheckUpdate");
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
                langEnglishItem.Checked = currentLanguage == "en-US";
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

        public void UpdateMinimizeToTrayOnCloseCheck(bool minimizeToTrayOnClose)
        {
            if (minimizeToTrayItem != null)
            {
                minimizeToTrayItem.Checked = minimizeToTrayOnClose;
            }
        }

        private void ToggleAutoBoot()
        {
            bool currentState = IsAutoBootEnabled;
            bool newState = !currentState;
            SetAutoBoot(newState);

            if (autoBootItem != null)
            {
                autoBootItem.Checked = newState;
            }

            AutoBootChanged?.Invoke(this, newState);
        }

        private void ToggleMinimizeToTrayOnClose()
        {
            if (minimizeToTrayItem != null)
            {
                minimizeToTrayItem.Checked = !minimizeToTrayItem.Checked;
                MinimizeToTrayOnCloseChanged?.Invoke(this, minimizeToTrayItem.Checked);
            }
        }

        private void ToggleAutoCheckUpdate()
        {
            if (autoCheckUpdateItem != null)
            {
                autoCheckUpdateItem.Checked = !autoCheckUpdateItem.Checked;
                AutoCheckUpdateChanged?.Invoke(this, autoCheckUpdateItem.Checked);
            }
        }

        public void UpdateAutoCheckUpdateCheck(bool autoCheckUpdate)
        {
            if (autoCheckUpdateItem != null)
            {
                autoCheckUpdateItem.Checked = autoCheckUpdate;
            }
        }

        private static void SetAutoBoot(bool enable)
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
                        // 添加 --autoboot 参数，用于区分开机自启动和手动启动
                        key.SetValue("MidiForwarder", $"\"{exePath}\" --autoboot");
                    }
                    else
                    {
                        key.DeleteValue("MidiForwarder", false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置开机自启动失败: {ex.Message}");
            }
        }

        public void ShowBalloonTip(string title, string text)
        {
            notifyIcon?.ShowBalloonTip(2000, title, text, ToolTipIcon.Info);
        }

        public void ShowTrayIcon()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = true;
            }
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
