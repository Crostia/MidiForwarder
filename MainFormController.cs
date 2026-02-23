using System.Globalization;

namespace MidiForwarder
{
    /// <summary>
    /// 主窗体控制器 - 处理主窗体的业务逻辑
    /// </summary>
    public class MainFormController : IDisposable
    {
        #region 字段和常量
        // 管理器实例
        private readonly ConfigManager configManager;
        private IMidiManager? midiManager;
        private TrayManager? trayManager;
        private MainFormLayout? layout;
        private UpdateManager? updateManager;
        private DeviceCacheManager? deviceCacheManager;
        private Form? mainForm;
        private MidiApiType currentApiType;

        // 启动参数
        private readonly bool isAutoStart;
        private readonly bool startInTray;
        private readonly bool startHidden;

        // 定时器
        private System.Windows.Forms.Timer? autoConnectRetryTimer;

        // 状态标记
        private bool isDisposed = false;
        private bool isInitialized = false;
        private bool isUserInteracted = false;
        private bool isExitFromTray = false;
        private int autoConnectRetryCount = 0;

        // 常量
        private const int MaxAutoConnectRetries = 3;
        #endregion

        #region 事件
        public event EventHandler? RequestShowWindow;
        public event EventHandler? RequestExit;
        #endregion

        #region 构造函数
        public MainFormController(string[] args)
        {
            // 解析命令行参数
            isAutoStart = args.Contains("--autoboot");
            startInTray = args.Contains("--tray");
            startHidden = args.Contains("--hidden");

            // 加载配置
            configManager = new ConfigManager();
            ApplyLanguageSetting();
        }
        #endregion

        #region 初始化
        /// <summary>
        /// 初始化主窗体
        /// </summary>
        public void Initialize(Form form)
        {
            mainForm = form;

            // 初始化UI组件
            InitializeComponents(form);

            // 设置应用日志的界面引用
            AppLog.SetLayout(layout);

            // 绑定事件处理器
            InitializeEventHandlers();

            // 标记初始化完成
            isInitialized = true;

            // 处理窗口显示/隐藏
            HandleWindowVisibility(form);

            // 自动连接（如果启用）
            TryAutoConnectOnStartup();

            // 后台检查更新
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                await CheckForUpdateOnStartupAsync();
            });
        }

        /// <summary>
        /// 初始化托盘图标
        /// </summary>
        public void InitializeTrayIcon()
        {
            trayManager = new TrayManager(
                configManager.Config.MinimizeToTrayOnClose,
                configManager.Config.Language,
                configManager.Config.AutoCheckUpdate);

            trayManager.ShowTrayIcon();

            trayManager.ShowWindowRequested += (s, e) =>
            {
                RequestShowWindow?.Invoke(this, EventArgs.Empty);
            };

            trayManager.ExitRequested += (s, e) =>
            {
                isExitFromTray = true;
                RequestExit?.Invoke(this, EventArgs.Empty);
            };
        }

        /// <summary>
        /// 初始化UI组件和管理器
        /// </summary>
        private void InitializeComponents(Form form)
        {
            // 使用工厂创建 MIDI 管理器，自动检测并使用最佳可用 API
            midiManager = MidiApiFactory.CreateMidiManagerWithFallback(configManager);
            currentApiType = midiManager is Midi20Manager ? MidiApiType.Midi20 : MidiApiType.Midi10;
            AppLog.Info($"使用 MIDI API: {(currentApiType == MidiApiType.Midi20 ? "Windows MIDI Services 2.0" : "MIDI 1.0 (NAudio)")}");

            layout = new MainFormLayout(form);
            deviceCacheManager = new DeviceCacheManager();

            // 如果托盘管理器已在InitializeTrayIconEarly中创建，则复用
            trayManager ??= new TrayManager(
                configManager.Config.MinimizeToTrayOnClose,
                configManager.Config.Language,
                configManager.Config.AutoCheckUpdate);

            updateManager = new UpdateManager();

            layout.AutoConnectCheckBox.Checked = configManager.Config.AutoConnectOnStartup;

            // 先执行一次手动刷新，获取初始设备列表
            var (inputDevices, outputDevices) = deviceCacheManager.ManualRefresh();
            UpdateDeviceListUI(inputDevices, outputDevices);

            // 初始化蓝牙过滤按钮状态
            UpdateFilterButtonState();
            UpdateOutputFilterButtonState();

            // 启动设备列表自动刷新
            deviceCacheManager.StartAutoRefresh();
        }

        /// <summary>
        /// 应用语言设置
        /// </summary>
        private void ApplyLanguageSetting()
        {
            var language = configManager.Config.Language;
            if (string.IsNullOrEmpty(language)) return;

            // 兼容旧配置：标准化语言代码
            language = language switch
            {
                "en" => "en-US",
                "zh" or "zh-Hans" or "zh-CHS" => "zh-CN",
                _ => language
            };

            configManager.UpdateLanguage(language);
            LocalizationManager.SetLanguage(language);
        }
        #endregion

        #region 事件绑定
        /// <summary>
        /// 初始化所有事件处理器
        /// </summary>
        private void InitializeEventHandlers()
        {
            if (layout is null || midiManager is null || trayManager is null || deviceCacheManager is null) return;

            BindUIEvents();
            BindMidiEvents();
            BindTrayEvents();
            BindDeviceCacheEvents();
        }

        private void BindUIEvents()
        {
            layout!.ConnectButtonClicked += (s, e) =>
            {
                isUserInteracted = true;
                OnConnectButtonClick();
            };

            layout.RefreshButtonClicked += (s, e) =>
            {
                isUserInteracted = true;
                OnRefreshButtonClick();
            };

            layout.AutoConnectChanged += (s, e) =>
            {
                isUserInteracted = true;
                configManager.UpdateAutoConnect(layout.AutoConnectCheckBox.Checked);
            };

            layout.InputSelectionChanged += (s, e) =>
            {
                isUserInteracted = true;
                UpdateConnectButtonState();
                UpdateFilterButtonState();
            };

            layout.OutputSelectionChanged += (s, e) =>
            {
                isUserInteracted = true;
                UpdateConnectButtonState();
                UpdateOutputFilterButtonState();
            };

            layout.DeviceSelectionChangedWhileConnected += (s, e) =>
            {
                isUserInteracted = true;
                DisconnectDevices();
                AppLog.Info(LocalizationManager.GetString("MsgDisconnectedDueToDeviceChange"));
            };

            layout.FilterBluetoothButtonClicked += (s, e) =>
            {
                isUserInteracted = true;
                OnFilterBluetoothButtonClick();
            };

            layout.OutputFilterBluetoothButtonClicked += (s, e) =>
            {
                isUserInteracted = true;
                OnOutputFilterBluetoothButtonClick();
            };
        }

        private void BindMidiEvents()
        {
            midiManager!.MessageReceived += (s, e) =>
            {
                AppLog.MidiMessage(LocalizationManager.GetString("LogMessageFormat",
                    e.Timestamp, e.MessageType, e.Channel, e.Data1, e.Data2));
            };

            midiManager.ErrorOccurred += (s, e) => AppLog.Error(e);

            midiManager.Connected += (s, e) =>
            {
                layout!.SetConnectedState(true);

                string inputDevice = layout.InputComboBox.SelectedItem?.ToString() ?? "";
                string outputDevice = layout.OutputComboBox.SelectedItem?.ToString() ?? "";

                configManager.UpdateSelectedDevices(inputDevice, outputDevice);
                AppLog.Info(LocalizationManager.GetString("MsgConnected"));

                deviceCacheManager?.StopAutoRefresh();
            };

            midiManager.Disconnected += (s, e) =>
            {
                layout!.SetConnectedState(false);
                AppLog.Info(LocalizationManager.GetString("MsgDisconnected"));
                deviceCacheManager?.StartAutoRefresh();
            };
        }

        private void BindTrayEvents()
        {
            trayManager!.ShowWindowRequested += (s, e) => RequestShowWindow?.Invoke(this, EventArgs.Empty);

            trayManager.AutoBootChanged += (s, e) =>
            {
                configManager.Config.AutoBoot = e;
                configManager.SaveConfig();
                AppLog.Info(e
                    ? LocalizationManager.GetString("MsgAutoBootEnabled")
                    : LocalizationManager.GetString("MsgAutoBootDisabled"));
            };

            trayManager.MinimizeToTrayOnCloseChanged += (s, e) =>
            {
                configManager.UpdateMinimizeToTrayOnClose(e);
            };

            trayManager.LanguageChanged += (s, language) =>
            {
                configManager.UpdateLanguage(language);
                LocalizationManager.SetLanguage(string.IsNullOrEmpty(language)
                    ? CultureInfo.CurrentUICulture.Name
                    : language);
                trayManager?.UpdateLanguage(language);
            };

            trayManager.ExitRequested += (s, e) =>
            {
                isExitFromTray = true;
                RequestExit?.Invoke(this, EventArgs.Empty);
            };

            trayManager.CheckUpdateRequested += async (s, e) =>
            {
                AppLog.Info(LocalizationManager.GetString("MsgCheckingForUpdates"));
                await PerformUpdateCheckAsync(false);
            };

            trayManager.AutoCheckUpdateChanged += (s, e) =>
            {
                configManager.UpdateAutoCheckUpdate(e);
                AppLog.Info(e
                    ? LocalizationManager.GetString("MsgAutoCheckUpdateEnabled")
                    : LocalizationManager.GetString("MsgAutoCheckUpdateDisabled"));
            };
        }

        private void BindDeviceCacheEvents()
        {
            // 订阅设备变更事件，自动刷新检测到变更时触发
            deviceCacheManager!.DevicesChanged += (s, e) =>
            {
                mainForm?.Invoke(() =>
                {
                    // 如果已连接，先断开连接
                    if (midiManager?.IsConnected == true)
                    {
                        DisconnectDevices();
                        AppLog.Info(LocalizationManager.GetString("MsgDisconnectedDueToDeviceChange"));
                    }

                    // 从缓存获取最新设备列表并更新UI
                    var (inputDevices, outputDevices) = deviceCacheManager.GetCachedDevices();
                    UpdateDeviceListUI(inputDevices, outputDevices);
                });
            };
        }
        #endregion

        #region 窗口管理
        /// <summary>
        /// 处理窗口显示或隐藏
        /// </summary>
        private void HandleWindowVisibility(Form form)
        {
            bool shouldHide = startHidden || (isAutoStart && startInTray);

            if (shouldHide)
            {
                form.Hide();
                trayManager?.ShowTrayIcon();

                if (!configManager.Config.HasShownTrayPrompt)
                {
                    trayManager?.ShowBalloonTip(
                        LocalizationManager.GetString("TrayBalloonTitle"),
                        LocalizationManager.GetString("TrayBalloonText"));
                    configManager.SetTrayPromptShown();
                }
            }
        }

        /// <summary>
        /// 应用启动时的窗口状态设置
        /// </summary>
        public void ApplyStartupWindowState(Form form)
        {
            bool shouldHide = startHidden || (isAutoStart && startInTray);

            if (shouldHide)
            {
                form.ShowInTaskbar = false;
                form.WindowState = FormWindowState.Minimized;
            }
        }

        /// <summary>
        /// 获取是否应该初始化托盘
        /// </summary>
        public bool ShouldInitializeTray => startHidden || (isAutoStart && startInTray);
        #endregion

        #region 设备刷新处理
        /// <summary>
        /// 更新设备列表UI
        /// </summary>
        private void UpdateDeviceListUI(List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices)
        {
            if (layout == null) return;

            string? savedInputName = layout.InputComboBox.SelectedItem?.ToString();
            string? savedOutputName = layout.OutputComboBox.SelectedItem?.ToString();

            layout.IsRefreshingDevices = true;

            try
            {
                layout.InputComboBox.Items.Clear();
                layout.OutputComboBox.Items.Clear();

                foreach (var device in inputDevices)
                    layout.InputComboBox.Items.Add(device.Name);

                foreach (var device in outputDevices)
                    layout.OutputComboBox.Items.Add(device.Name);

                RestoreSelectionByName(savedInputName, savedOutputName, inputDevices, outputDevices);
                UpdateConnectButtonState();
            }
            finally
            {
                layout.IsRefreshingDevices = false;
            }
        }

        private void RestoreSelectionByName(string? inputName, string? outputName,
            List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices)
        {
            if (!string.IsNullOrEmpty(inputName))
            {
                var inputIndex = inputDevices.FindIndex(d => d.Name == inputName);
                if (inputIndex >= 0)
                    layout!.InputComboBox.SelectedIndex = inputIndex;
                else if (layout!.InputComboBox.Items.Count > 0)
                    layout.InputComboBox.SelectedIndex = 0;
            }
            else if (layout!.InputComboBox.Items.Count > 0)
            {
                layout.InputComboBox.SelectedIndex = 0;
            }

            if (!string.IsNullOrEmpty(outputName))
            {
                var outputIndex = outputDevices.FindIndex(d => d.Name == outputName);
                if (outputIndex >= 0)
                    layout!.OutputComboBox.SelectedIndex = outputIndex;
                else if (layout!.OutputComboBox.Items.Count > 0)
                    layout.OutputComboBox.SelectedIndex = 0;
            }
            else if (layout!.OutputComboBox.Items.Count > 0)
            {
                layout.OutputComboBox.SelectedIndex = 0;
            }
        }
        #endregion

        #region MIDI连接
        private void OnConnectButtonClick()
        {
            if (midiManager is null) return;

            if (!midiManager.IsConnected)
                ConnectDevices();
            else
                DisconnectDevices();
        }

        private void OnRefreshButtonClick()
        {
            layout!.ClearLog();
            var (inputDevices, outputDevices) = deviceCacheManager!.ManualRefresh();
            UpdateDeviceListUI(inputDevices, outputDevices);
            AppLog.Info(LocalizationManager.GetString("MsgDevicesRefreshed"));
        }

        private void OnFilterBluetoothButtonClick()
        {
            var selectedDevice = layout!.InputComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDevice))
            {
                AppLog.Info(LocalizationManager.GetString("LogSelectInputFirst"));
                return;
            }

            // 获取设备名称（不含ID）
            var deviceName = ConfigManager.ParseDeviceName(selectedDevice);

            // 检查是否已在排除名单中
            var exclusions = configManager.GetWiredDeviceExclusions();
            bool isInExclusionList = exclusions.Any(e =>
                string.Equals(e, deviceName, StringComparison.OrdinalIgnoreCase));

            if (isInExclusionList)
            {
                // 从排除名单移除
                if (configManager.RemoveFromWiredDeviceExclusions(selectedDevice))
                {
                    AppLog.Info(string.Format(LocalizationManager.GetString("LogRemovedFromExclusionList"), deviceName));
                    AppLog.Info(LocalizationManager.GetString("LogDeviceWillResumeBluetooth"));
                }
            }
            else
            {
                // 添加到排除名单
                if (configManager.AddToWiredDeviceExclusions(selectedDevice))
                {
                    AppLog.Info(string.Format(LocalizationManager.GetString("LogAddedToExclusionList"), deviceName));
                    AppLog.Info(LocalizationManager.GetString("LogDeviceWillNotBeBluetooth"));
                }
            }

            // 更新按钮状态
            UpdateFilterButtonState();

            // 刷新设备列表以应用更改
            var (inputDevices, outputDevices) = deviceCacheManager!.ManualRefresh();
            UpdateDeviceListUI(inputDevices, outputDevices);
        }

        private void UpdateFilterButtonState()
        {
            var selectedDevice = layout!.InputComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDevice))
            {
                layout.UpdateFilterButtonState(false);
                return;
            }

            var deviceName = ConfigManager.ParseDeviceName(selectedDevice);
            var exclusions = configManager.GetWiredDeviceExclusions();
            bool isInExclusionList = exclusions.Any(e =>
                string.Equals(e, deviceName, StringComparison.OrdinalIgnoreCase));

            layout.UpdateFilterButtonState(isInExclusionList);
        }

        private void OnOutputFilterBluetoothButtonClick()
        {
            var selectedDevice = layout!.OutputComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDevice))
            {
                AppLog.Info(LocalizationManager.GetString("LogSelectOutputFirst"));
                return;
            }

            // 获取设备名称（不含ID）
            var deviceName = ConfigManager.ParseDeviceName(selectedDevice);

            // 检查是否已在排除名单中
            var exclusions = configManager.GetWiredDeviceExclusions();
            bool isInExclusionList = exclusions.Any(e =>
                string.Equals(e, deviceName, StringComparison.OrdinalIgnoreCase));

            if (isInExclusionList)
            {
                // 从排除名单移除
                if (configManager.RemoveFromWiredDeviceExclusions(selectedDevice))
                {
                    AppLog.Info(string.Format(LocalizationManager.GetString("LogRemovedFromExclusionList"), deviceName));
                    AppLog.Info(LocalizationManager.GetString("LogDeviceWillResumeBluetooth"));
                }
            }
            else
            {
                // 添加到排除名单
                if (configManager.AddToWiredDeviceExclusions(selectedDevice))
                {
                    AppLog.Info(string.Format(LocalizationManager.GetString("LogAddedToExclusionList"), deviceName));
                    AppLog.Info(LocalizationManager.GetString("LogDeviceWillNotBeBluetooth"));
                }
            }

            // 更新按钮状态
            UpdateOutputFilterButtonState();

            // 刷新设备列表以应用更改
            var (inputDevices, outputDevices) = deviceCacheManager!.ManualRefresh();
            UpdateDeviceListUI(inputDevices, outputDevices);
        }

        private void UpdateOutputFilterButtonState()
        {
            var selectedDevice = layout!.OutputComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDevice))
            {
                layout.UpdateOutputFilterButtonState(false);
                return;
            }

            var deviceName = ConfigManager.ParseDeviceName(selectedDevice);
            var exclusions = configManager.GetWiredDeviceExclusions();
            bool isInExclusionList = exclusions.Any(e =>
                string.Equals(e, deviceName, StringComparison.OrdinalIgnoreCase));

            layout.UpdateOutputFilterButtonState(isInExclusionList);
        }

        private void ConnectDevices()
        {
            if (midiManager is null || layout is null || deviceCacheManager is null) return;

            // 获取选中的设备名称
            string? inputName = layout.InputComboBox.SelectedItem?.ToString();
            string? outputName = layout.OutputComboBox.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(inputName) || string.IsNullOrEmpty(outputName)) return;

            // 从缓存查找真实的设备ID
            string? inputId = deviceCacheManager.FindInputDeviceIdByName(inputName);
            string? outputId = deviceCacheManager.FindOutputDeviceIdByName(outputName);

            if (string.IsNullOrEmpty(inputId) || string.IsNullOrEmpty(outputId))
            {
                AppLog.Error(string.Format(LocalizationManager.GetString("LogDeviceNotFound"), inputName, outputName));
                return;
            }

            midiManager.Connect(inputId, outputId);
        }

        private void DisconnectDevices()
        {
            midiManager?.Disconnect();
        }

        private void UpdateConnectButtonState()
    {
        bool canConnect = layout!.InputComboBox.SelectedIndex >= 0 && layout.OutputComboBox.SelectedIndex >= 0;
        layout.UpdateConnectButtonState(canConnect);
    }
        #endregion

        #region 自动连接
        private void TryAutoConnectOnStartup()
        {
            if (!configManager.Config.AutoConnectOnStartup) return;
            if (string.IsNullOrEmpty(configManager.Config.SelectedInputDevice)) return;
            if (string.IsNullOrEmpty(configManager.Config.SelectedOutputDevice)) return;

            var (inputDevices, outputDevices) = deviceCacheManager?.GetCachedDevices()
                ?? (new List<MidiDeviceInfo>(), new List<MidiDeviceInfo>());

            TryAutoConnectWithRetry(inputDevices, outputDevices);
        }

        private void TryAutoConnectWithRetry(List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices)
        {
            var result = TryConnectSavedDevices(inputDevices, outputDevices);
            if (result) return;

            autoConnectRetryCount = 0;
            isUserInteracted = false;
            var retryInterval = configManager.Config.AutoConnectRetryIntervalSeconds;

            // 首次连接失败提示
            var savedInputDevice = configManager.Config.SelectedInputDevice;
            var savedOutputDevice = configManager.Config.SelectedOutputDevice;
            AppLog.Info(string.Format(LocalizationManager.GetString("LogAutoConnectFailed"), savedInputDevice, savedOutputDevice));
            AppLog.Info(string.Format(LocalizationManager.GetString("LogAutoConnectRetryIn"), retryInterval));

            autoConnectRetryTimer = new System.Windows.Forms.Timer
            {
                Interval = retryInterval * 1000
            };

            autoConnectRetryTimer.Tick += (s, e) =>
            {
                if (isUserInteracted)
                {
                    AppLog.Info(LocalizationManager.GetString("LogAutoConnectCancelled"));
                    autoConnectRetryTimer?.Stop();
                    autoConnectRetryTimer?.Dispose();
                    autoConnectRetryTimer = null;
                    return;
                }

                autoConnectRetryCount++;

                var (currentInputDevices, currentOutputDevices) = deviceCacheManager?.GetCachedDevices()
                    ?? (new List<MidiDeviceInfo>(), new List<MidiDeviceInfo>());

                if (TryConnectSavedDevices(currentInputDevices, currentOutputDevices))
                {
                    autoConnectRetryTimer?.Stop();
                    autoConnectRetryTimer?.Dispose();
                    autoConnectRetryTimer = null;
                }
                else if (autoConnectRetryCount >= MaxAutoConnectRetries)
                {
                    AppLog.Error(string.Format(LocalizationManager.GetString("LogAutoConnectFailedAfterRetries"), MaxAutoConnectRetries));
                    autoConnectRetryTimer?.Stop();
                    autoConnectRetryTimer?.Dispose();
                    autoConnectRetryTimer = null;
                }
                else
                {
                    AppLog.Info(string.Format(LocalizationManager.GetString("LogAutoConnectRetryFailed"), autoConnectRetryCount, retryInterval, autoConnectRetryCount + 1));
                }
            };

            autoConnectRetryTimer.Start();
        }

        private bool TryConnectSavedDevices(List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices)
        {
            var savedInputDevice = configManager.Config.SelectedInputDevice;
            var savedOutputDevice = configManager.Config.SelectedOutputDevice;

            string? matchedInputId = null;
            string? matchedOutputId = null;

            if (!string.IsNullOrEmpty(savedInputDevice))
            {
                var matchedInput = inputDevices.FirstOrDefault(d => d.Name == savedInputDevice);
                if (matchedInput != null) matchedInputId = matchedInput.Id;
            }

            if (!string.IsNullOrEmpty(savedOutputDevice))
            {
                var matchedOutput = outputDevices.FirstOrDefault(d => d.Name == savedOutputDevice);
                if (matchedOutput != null) matchedOutputId = matchedOutput.Id;
            }

            if (!string.IsNullOrEmpty(matchedInputId) && !string.IsNullOrEmpty(matchedOutputId))
            {
                // 自动连接：先更新UI选中项，再连接
                SelectDevicesById(matchedInputId, matchedOutputId, inputDevices, outputDevices);
                midiManager?.Connect(matchedInputId, matchedOutputId);
                return true;
            }

            return false;
        }

        private void SelectDevicesById(string inputId, string outputId, List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices)
        {
            var inputIndex = inputDevices.FindIndex(d => d.Id == inputId);
            var outputIndex = outputDevices.FindIndex(d => d.Id == outputId);

            if (layout == null) return;

            if (inputIndex >= 0 && inputIndex < layout.InputComboBox.Items.Count)
                layout.InputComboBox.SelectedIndex = inputIndex;

            if (outputIndex >= 0 && outputIndex < layout.OutputComboBox.Items.Count)
                layout.OutputComboBox.SelectedIndex = outputIndex;
        }
        #endregion

        #region 更新检查
        private async Task CheckForUpdateOnStartupAsync()
        {
            if (!configManager.Config.AutoCheckUpdate) return;

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
                configManager.UpdateLastUpdateCheck(DateTime.Now);

                if (result.IsError)
                {
                    if (!isSilent)
                    {
                        ShowUpdateError(result.ErrorMessage);
                    }
                    return;
                }

                if (result.HasUpdate)
                {
                    ShowUpdateDialog(result);
                }
                else if (!isSilent)
                {
                    ShowNoUpdateMessage();
                }
            }
            catch (Exception ex)
            {
                if (!isSilent)
                {
                    ShowUpdateError($"{LocalizationManager.GetString("UpdateCheckError")}: {ex.Message}");
                }
            }
        }

        private void ShowUpdateDialog(UpdateCheckResult result)
        {
            if (configManager.Config.IgnoredVersion == result.LatestVersion) return;

            var (shouldDownload, shouldIgnore) = UpdateDialog.ShowUpdateDialog(
                result.CurrentVersion,
                result.LatestVersion,
                result.ReleaseNotes ?? "");

            if (shouldIgnore)
            {
                configManager.UpdateIgnoredVersion(result.LatestVersion);
            }

            if (shouldDownload)
            {
                UpdateManager.OpenReleasePage(result.DownloadUrl);
            }
        }

        private static void ShowUpdateError(string message)
        {
            MessageBox.Show(message,
                LocalizationManager.GetString("UpdateCheckErrorTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static void ShowNoUpdateMessage()
        {
            MessageBox.Show(LocalizationManager.GetString("UpdateNoUpdateMessage"),
                LocalizationManager.GetString("UpdateCheckTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #endregion

        #region 关闭处理
        /// <summary>
        /// 处理窗体关闭事件
        /// </summary>
        public bool HandleFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // 从托盘退出时不显示确认对话框，直接关闭
                if (isExitFromTray)
                {
                    isExitFromTray = false; // 重置标记，下次从主页面关闭时仍会提示
                    return true; // 继续关闭流程
                }

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
                        configManager.UpdateMinimizeToTrayOnClose(false);
                        trayManager?.UpdateMinimizeToTrayOnCloseCheck(false);
                    }
                    else if (result == DialogResult.Yes)
                    {
                        configManager.UpdateMinimizeToTrayOnClose(true);
                        trayManager?.UpdateMinimizeToTrayOnCloseCheck(true);
                    }

                    configManager.SetTrayPromptShown();
                }

                // 根据设置决定是退出还是最小化到托盘
                if (configManager.Config.MinimizeToTrayOnClose)
                {
                    e.Cancel = true;
                    return false; // 不退出，只是隐藏
                }
            }

            return true; // 继续关闭流程
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (isDisposed) return;

            autoConnectRetryTimer?.Stop();
            autoConnectRetryTimer?.Dispose();

            deviceCacheManager?.Dispose();
            midiManager?.Dispose();
            trayManager?.Dispose();
            updateManager?.Dispose();

            isDisposed = true;
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
