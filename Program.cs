using System.Globalization;

namespace MidiForwarder
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    public partial class MainForm : Form
    {
        private ConfigManager? configManager;
        private MidiManager? midiManager;
        private TrayManager? trayManager;
        private MainFormLayout? layout;
        private bool isClosing = false;

        public MainForm()
        {
            InitializeComponents();
            ApplyLanguageSetting();
            InitializeEventHandlers();
            RefreshDeviceLists();

            // 处理启动时最小化到托盘
            // 只有在开机自启动且启用了最小化到托盘时才隐藏窗口
            if (configManager?.Config.MinimizeToTray == true &&
                configManager.Config.AutoStart &&
                TrayManager.IsAutoStartEnabled)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
                Hide();
            }

            // 自动连接
            if (configManager?.Config.AutoConnectOnStartup == true &&
                configManager.Config.SelectedInputDeviceId >= 0 &&
                configManager.Config.SelectedOutputDeviceId >= 0)
            {
                SelectDevicesById(configManager.Config.SelectedInputDeviceId, configManager.Config.SelectedOutputDeviceId);
                ConnectDevices();
            }
        }

        private void InitializeComponents()
        {
            // 初始化各个管理器
            configManager = new ConfigManager();
            midiManager = new MidiManager();
            layout = new MainFormLayout(this);
            trayManager = new TrayManager(configManager!.Config.MinimizeToTray, configManager.Config.Language);

            // 设置自动连接复选框状态
            layout!.AutoConnectCheckBox.Checked = configManager!.Config.AutoConnectOnStartup;

            // 订阅窗体事件
            FormClosing += MainForm_FormClosing;
        }

        private void ApplyLanguageSetting()
        {
            if (configManager?.Config == null) return;

            var language = configManager.Config.Language;
            if (!string.IsNullOrEmpty(language))
            {
                // 使用配置的语言
                LocalizationManager.SetLanguage(language);
            }
            // 如果 Language 为空，则使用系统默认语言
        }

        private void InitializeEventHandlers()
        {
            if (layout is null || midiManager is null || trayManager is null || configManager is null) return;

            // UI事件
            layout.ConnectButtonClicked += (s, e) => OnConnectButtonClick();
            layout.RefreshButtonClicked += (s, e) => OnRefreshButtonClick();
            layout.AutoConnectChanged += (s, e) =>
            {
                configManager?.UpdateAutoConnect(layout.AutoConnectCheckBox.Checked);
            };
            layout.InputSelectionChanged += (s, e) => UpdateConnectButtonState();
            layout.OutputSelectionChanged += (s, e) => UpdateConnectButtonState();

            // MIDI管理器事件
            midiManager.MessageReceived += (s, e) =>
            {
                layout!.LogMessage(LocalizationManager.GetString("LogMessageFormat", e.Timestamp, e.MessageType, e.Channel, e.Data1, e.Data2));
            };

            midiManager.ErrorOccurred += (s, e) =>
            {
                layout!.LogMessage(e);
            };

            midiManager.Connected += (s, e) =>
            {
                layout!.SetConnectedState(true);
                configManager?.UpdateSelectedDevices(midiManager.SelectedInputDeviceId, midiManager.SelectedOutputDeviceId);
                layout!.LogMessage(LocalizationManager.GetString("MsgConnected"));
            };

            midiManager.Disconnected += (s, e) =>
            {
                layout!.SetConnectedState(false);
                layout!.LogMessage(LocalizationManager.GetString("MsgDisconnected"));
            };

            // 托盘管理器事件
            trayManager.ShowWindowRequested += (s, e) => ShowFromTray();
            trayManager.AutoStartChanged += (s, e) =>
            {
                if (configManager is null) return;
                configManager.Config.AutoStart = e;
                configManager.SaveConfig();
                layout.LogMessage(e ? LocalizationManager.GetString("MsgAutoStartEnabled") : LocalizationManager.GetString("MsgAutoStartDisabled"));
            };
            trayManager.MinimizeToTrayChanged += (s, e) =>
            {
                configManager?.UpdateMinimizeToTray(e);
            };
            trayManager.LanguageChanged += (s, language) =>
            {
                configManager?.UpdateLanguage(language);
                LocalizationManager.SetLanguage(string.IsNullOrEmpty(language) ? CultureInfo.CurrentUICulture.Name : language);
                trayManager?.UpdateLanguage(language);
            };
            trayManager.ExitRequested += (s, e) => ExitApplication();
        }

        private void OnConnectButtonClick()
        {
            if (midiManager is null) return;
            if (!midiManager.IsConnected)
            {
                ConnectDevices();
            }
            else
            {
                DisconnectDevices();
            }
        }

        private void OnRefreshButtonClick()
        {
            layout!.ClearLog();
            RefreshDeviceLists();
            layout!.LogMessage(LocalizationManager.GetString("MsgDevicesRefreshed"));
        }

        private void ConnectDevices()
        {
            if (midiManager is null) return;
            int inputId = layout!.InputComboBox.SelectedIndex;
            int outputId = layout.OutputComboBox.SelectedIndex;

            if (inputId < 0 || outputId < 0)
                return;

            midiManager.Connect(inputId, outputId);
        }

        private void DisconnectDevices()
        {
            midiManager?.Disconnect();
        }

        private void RefreshDeviceLists()
        {
            layout!.InputComboBox.Items.Clear();
            layout.OutputComboBox.Items.Clear();

            var inputDevices = MidiManager.GetInputDevices();
            var outputDevices = MidiManager.GetOutputDevices();

            foreach (var device in inputDevices)
            {
                layout.InputComboBox.Items.Add(device.Name);
            }

            foreach (var device in outputDevices)
            {
                layout.OutputComboBox.Items.Add(device.Name);
            }

            if (layout.InputComboBox.Items.Count > 0)
                layout.InputComboBox.SelectedIndex = 0;

            if (layout.OutputComboBox.Items.Count > 0)
                layout.OutputComboBox.SelectedIndex = 0;
        }

        private void SelectDevicesById(int inputId, int outputId)
        {
            if (inputId < layout!.InputComboBox.Items.Count)
                layout.InputComboBox.SelectedIndex = inputId;

            if (outputId < layout.OutputComboBox.Items.Count)
                layout.OutputComboBox.SelectedIndex = outputId;
        }

        private void UpdateConnectButtonState()
        {
            bool canConnect = layout!.InputComboBox.SelectedIndex >= 0 && layout.OutputComboBox.SelectedIndex >= 0;
            layout.UpdateConnectButtonState(canConnect);
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            isClosing = true;
            Application.Exit();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!isClosing && e.CloseReason == CloseReason.UserClosing)
            {
                if (configManager is null) return;

                // 首次关闭时显示确认弹窗
                if (!configManager.Config.HasShownTrayPrompt)
                {
                    var result = MessageBox.Show(
                        LocalizationManager.GetString("CloseConfirmMessage"),
                        LocalizationManager.GetString("CloseConfirmTitle"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1);

                    if (result == DialogResult.No)
                    {
                        // 用户选择直接退出，禁用最小化到托盘
                        configManager.UpdateMinimizeToTray(false);
                        trayManager?.UpdateMinimizeToTrayCheck(false);
                    }
                    else if (result == DialogResult.Yes)
                    {
                        // 用户选择最小化到托盘
                        configManager.UpdateMinimizeToTray(true);
                        trayManager?.UpdateMinimizeToTrayCheck(true);
                    }

                    configManager.SetTrayPromptShown();
                }

                // 根据设置决定是退出还是最小化到托盘
                if (configManager.Config.MinimizeToTray)
                {
                    e.Cancel = true;
                    Hide();
                    trayManager?.ShowBalloonTip(
                        LocalizationManager.GetString("TrayBalloonTitle"),
                        LocalizationManager.GetString("TrayBalloonText"));
                }
                else
                {
                    // 直接退出
                    midiManager?.Dispose();
                    trayManager?.Dispose();
                }
            }
            else
            {
                midiManager?.Dispose();
                trayManager?.Dispose();
            }
        }


    }
}
