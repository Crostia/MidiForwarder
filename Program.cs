using System.Globalization;

namespace MidiForwarder
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(args));
        }
    }

    public partial class MainForm : Form
    {
        private readonly ConfigManager? configManager;
        private MidiManager? midiManager;
        private TrayManager? trayManager;
        private MainFormLayout? layout;
        private UpdateManager? updateManager;
        private bool isClosing = false;
        private readonly bool isAutoStart;

        public MainForm(string[] args)
        {
            // 检查是否是通过开机自启动启动的（通过命令行参数判断）
            isAutoStart = args.Contains("--autostart");

            // 先加载配置并应用语言设置，再初始化UI
            configManager = new ConfigManager();
            ApplyLanguageSetting();

            InitializeComponents();
            InitializeEventHandlers();
            var (inputDevices, outputDevices) = RefreshDeviceLists();

            // 处理启动时最小化到托盘
            // 只有在开机自启动且启用了最小化到托盘时才隐藏窗口
            if (isAutoStart &&
                configManager?.Config.MinimizeToTray == true &&
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
                SelectDevicesById(configManager.Config.SelectedInputDeviceId, configManager.Config.SelectedOutputDeviceId, inputDevices, outputDevices);
                ConnectDevices();
            }

            // 启动时检查更新（如果不是开机自启动，或者距离上次检查已经超过1天）
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // 延迟2秒，等待窗口加载完成
                await CheckForUpdateOnStartupAsync();
            });
        }

        private async Task CheckForUpdateOnStartupAsync()
        {
            if (configManager?.Config.AutoCheckUpdate != true) return;

            // 检查是否已经检查过更新（每天最多检查一次）
            var lastCheck = configManager.Config.LastUpdateCheck;
            if (DateTime.Now - lastCheck < TimeSpan.FromDays(1)) return;

            await PerformUpdateCheckAsync(true);
        }

        private async Task PerformUpdateCheckAsync(bool isSilent = false)
        {
            if (updateManager == null) return;

            try
            {
                var result = await updateManager.CheckForUpdateAsync();

                // 更新上次检查时间
                configManager?.UpdateLastUpdateCheck(DateTime.Now);

                if (result.IsError)
                {
                    if (!isSilent)
                    {
                        Invoke(() =>
                        {
                            MessageBox.Show(result.ErrorMessage, LocalizationManager.GetString("UpdateCheckErrorTitle"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        });
                    }
                    return;
                }

                if (result.HasUpdate)
                {
                    Invoke(() =>
                    {
                        ShowUpdateDialog(result);
                    });
                }
                else if (!isSilent)
                {
                    Invoke(() =>
                    {
                        MessageBox.Show(LocalizationManager.GetString("UpdateNoUpdateMessage"),
                            LocalizationManager.GetString("UpdateCheckTitle"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                if (!isSilent)
                {
                    Invoke(() =>
                    {
                        MessageBox.Show($"{LocalizationManager.GetString("UpdateCheckError")}: {ex.Message}",
                            LocalizationManager.GetString("UpdateCheckErrorTitle"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }
        }

        private static void ShowUpdateDialog(UpdateCheckResult result)
        {
            var message = string.Format(LocalizationManager.GetString("UpdateAvailableMessage"),
                result.CurrentVersion, result.LatestVersion);

            if (!string.IsNullOrEmpty(result.ReleaseNotes))
            {
                message += "\n\n" + LocalizationManager.GetString("UpdateReleaseNotes") + "\n" + result.ReleaseNotes;
            }

            var dialogResult = MessageBox.Show(message, LocalizationManager.GetString("UpdateAvailableTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (dialogResult == DialogResult.Yes)
            {
                UpdateManager.OpenReleasePage(result.DownloadUrl);
            }
        }

        private void InitializeComponents()
        {
            // 初始化各个管理器（configManager已在构造函数中创建）
            midiManager = new MidiManager();
            layout = new MainFormLayout(this);
            trayManager = new TrayManager(configManager!.Config.MinimizeToTray, configManager.Config.Language, configManager.Config.AutoCheckUpdate);
            updateManager = new UpdateManager();

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

            // 更新检查事件
            trayManager.CheckUpdateRequested += async (s, e) =>
            {
                layout!.LogMessage(LocalizationManager.GetString("MsgCheckingForUpdates"));
                await PerformUpdateCheckAsync(false);
            };
            trayManager.AutoCheckUpdateChanged += (s, e) =>
            {
                configManager?.UpdateAutoCheckUpdate(e);
                layout.LogMessage(e ? LocalizationManager.GetString("MsgAutoCheckUpdateEnabled") : LocalizationManager.GetString("MsgAutoCheckUpdateDisabled"));
            };
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

        private (List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices) RefreshDeviceLists()
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

            return (inputDevices, outputDevices);
        }

        private void SelectDevicesById(int inputId, int outputId, List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices)
        {
            // 根据设备 ID 找到对应的下拉框索引
            var inputIndex = inputDevices.FindIndex(d => d.Id == inputId);
            var outputIndex = outputDevices.FindIndex(d => d.Id == outputId);

            if (layout == null) return;

            if (inputIndex >= 0 && inputIndex < layout.InputComboBox.Items.Count)
                layout.InputComboBox.SelectedIndex = inputIndex;

            if (outputIndex >= 0 && outputIndex < layout.OutputComboBox.Items.Count)
                layout.OutputComboBox.SelectedIndex = outputIndex;
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
            // 先释放资源，再关闭窗体
            midiManager?.Dispose();
            trayManager?.Dispose();
            updateManager?.Dispose();
            // 使用 Close 而不是 Application.Exit 避免集合修改异常
            Close();
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
                    updateManager?.Dispose();
                }
            }
            else
            {
                midiManager?.Dispose();
                trayManager?.Dispose();
                updateManager?.Dispose();
            }
        }
    }
}
