using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace MidiForwarder
{
    internal static partial class Program
    {
        private const string MutexName = "MidiForwarder_SingleInstance_Mutex";
        private static Mutex? mutex;
        private static uint showWindowMessage;

        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            // 注册自定义窗口消息，用于进程间通信
            showWindowMessage = RegisterWindowMessage("MidiForwarder_ShowWindow");

            // 尝试创建互斥锁以实现单实例控制
            bool createdNew;
            try
            {
                mutex = new Mutex(true, MutexName, out createdNew);
            }
            catch (AbandonedMutexException)
            {
                // 互斥锁被遗弃（之前的实例崩溃未释放），获取所有权
                mutex = new Mutex(true, MutexName, out _);
                createdNew = true;
            }

            if (!createdNew)
            {
                // 已有实例在运行，发送消息激活已有实例窗口
                ShowExistingInstanceWindow();
                return;
            }

            try
            {
                Application.Run(new MainForm(args, showWindowMessage));
            }
            finally
            {
                // 释放互斥锁
                mutex?.ReleaseMutex();
                mutex?.Dispose();
            }
        }

        /// <summary>
        /// 向已有实例发送消息，激活其窗口
        /// </summary>
        private static void ShowExistingInstanceWindow()
        {
            try
            {
                // 尝试通过进程名查找窗口句柄（最可靠的方法）
                var windowHandle = FindWindowByProcess();

                if (windowHandle != IntPtr.Zero)
                {
                    // 发送自定义消息通知已有实例显示窗口
                    // 使用 PostMessage 确保非阻塞
                    PostMessage(windowHandle, showWindowMessage, IntPtr.Zero, IntPtr.Zero);

                    // 同时尝试激活窗口（处理旧版本或消息失败的情况）
                    ShowWindow(windowHandle, SW_RESTORE);
                    SetForegroundWindow(windowHandle);
                }
            }
            catch { }
        }

        /// <summary>
        /// 通过进程名查找窗口句柄
        /// </summary>
        private static IntPtr FindWindowByProcess()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processName = Path.GetFileNameWithoutExtension(currentProcess.ProcessName);
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    // 尝试获取主窗口句柄（即使窗口被隐藏，主窗口句柄通常也存在）
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        return process.MainWindowHandle;
                    }

                    // 如果 MainWindowHandle 为 0，尝试枚举进程的所有窗口
                    // 包括不可见窗口，因为窗口可能被隐藏到托盘
                    var handle = IntPtr.Zero;
                    EnumWindows((hWnd, lParam) =>
                    {
                        uint threadId = GetWindowThreadProcessId(hWnd, out uint processId);
                        // 检查 GetWindowThreadProcessId 是否成功（返回非零线程ID）且进程ID匹配
                        if (threadId != 0 && processId == process.Id)
                        {
                            // 不检查窗口是否可见，因为窗口可能被隐藏
                            // 只检查是否是顶层窗口
                            if (GetParent(hWnd) == IntPtr.Zero)
                            {
                                handle = hWnd;
                                return false; // 停止枚举
                            }
                        }
                        return true; // 继续枚举
                    }, IntPtr.Zero);

                    if (handle != IntPtr.Zero)
                    {
                        return handle;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private const int SW_RESTORE = 9;

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint RegisterWindowMessage(string lpString);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [LibraryImport("user32.dll")]
        private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindowVisible(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        private static partial IntPtr GetParent(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
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
        private readonly uint showWindowMessage;

        private System.Windows.Forms.Timer? autoConnectRetryTimer;
        private int autoConnectRetryCount = 0;
        private const int MaxAutoConnectRetries = 100; // 最大重试次数（约50分钟）

        public MainForm(string[] args, uint showWindowMessageId = 0)
        {
            // 检查是否是通过开机自启动启动的（通过命令行参数判断）
            isAutoStart = args.Contains("--autostart");
            showWindowMessage = showWindowMessageId;

            // 先加载配置并应用语言设置，再初始化UI
            configManager = new ConfigManager();
            ApplyLanguageSetting();

            // 如果是开机自启动，先延迟再初始化
            if (isAutoStart && configManager?.Config.AutoBootDelayMinutes > 0)
            {
                InitializeDelayedStartup(configManager.Config.AutoBootDelayMinutes);
            }
            else
            {
                InitializeMainForm();
            }
        }

        private void InitializeDelayedStartup(int delayMinutes)
        {
            // 静默启动：在后台等待，不显示窗口
            // 使用同步等待，保持应用程序不显示任何UI
            Thread.Sleep(delayMinutes * 60 * 1000);
            InitializeMainForm();
        }

        private void InitializeMainForm()
        {
            InitializeComponents();
            InitializeEventHandlers();

            // 注册自定义窗口消息处理（用于单实例激活）
            if (showWindowMessage != 0)
            {
                RegisterShowWindowMessageHandler();
            }

            var (inputDevices, outputDevices) = RefreshDeviceLists();

            // 处理启动时最小化到托盘
            // 只有开机自启动时才根据设置决定是否最小化，手动启动时始终显示窗口
            bool shouldMinimize = isAutoStart && configManager?.Config.StartMinimizedOnAutoStart == true;

            if (shouldMinimize && configManager?.Config.MinimizeToTrayOnClose == true)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
                Hide();
            }

            // 自动连接（支持重试）
            if (configManager?.Config.AutoConnectOnStartup == true &&
                configManager.Config.SelectedInputDeviceId >= 0 &&
                configManager.Config.SelectedOutputDeviceId >= 0)
            {
                TryAutoConnectWithRetry(inputDevices, outputDevices);
            }

            // 启动时检查更新（如果不是开机自启动，或者距离上次检查已经超过1天）
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // 延迟2秒，等待窗口加载完成
                await CheckForUpdateOnStartupAsync();
            });
        }

        private void TryAutoConnectWithRetry(List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices)
        {
            // 先尝试立即连接
            if (TryConnectSavedDevices(inputDevices, outputDevices))
            {
                return; // 连接成功，不需要重试
            }

            // 连接失败，启动重试定时器（静默重试，不显示日志）
            autoConnectRetryCount = 0;
            var retryInterval = configManager?.Config.AutoConnectRetryIntervalSeconds ?? 30;

            autoConnectRetryTimer = new System.Windows.Forms.Timer
            {
                Interval = retryInterval * 1000 // 转换为毫秒
            };

            autoConnectRetryTimer.Tick += (s, e) =>
            {
                autoConnectRetryCount++;

                // 刷新设备列表
                var (newInputDevices, newOutputDevices) = RefreshDeviceLists();

                if (TryConnectSavedDevices(newInputDevices, newOutputDevices))
                {
                    // 连接成功，停止定时器
                    autoConnectRetryTimer?.Stop();
                    autoConnectRetryTimer?.Dispose();
                    autoConnectRetryTimer = null;
                    // 静默模式：不输出日志
                }
                else if (autoConnectRetryCount >= MaxAutoConnectRetries)
                {
                    // 达到最大重试次数，停止重试
                    autoConnectRetryTimer?.Stop();
                    autoConnectRetryTimer?.Dispose();
                    autoConnectRetryTimer = null;
                    // 静默模式：不输出日志
                }
                // 继续重试（静默模式，不输出日志）
            };

            autoConnectRetryTimer.Start();
        }

        private bool TryConnectSavedDevices(List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices)
        {
            if (configManager == null) return false;

            var savedInputId = configManager.Config.SelectedInputDeviceId;
            var savedOutputId = configManager.Config.SelectedOutputDeviceId;

            // 检查保存的设备是否存在于当前设备列表中
            var inputExists = inputDevices.Any(d => d.Id == savedInputId);
            var outputExists = outputDevices.Any(d => d.Id == savedOutputId);

            if (inputExists && outputExists)
            {
                SelectDevicesById(savedInputId, savedOutputId, inputDevices, outputDevices);
                ConnectDevices();
                return true;
            }

            return false;
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

        private void ShowUpdateDialog(UpdateCheckResult result)
        {
            // 检查是否被忽略的版本
            if (configManager?.Config.IgnoredVersion == result.LatestVersion)
            {
                return; // 此版本已被忽略，不显示对话框
            }

            // 使用自定义对话框替代 MessageBox，支持滚动显示长文本
            var (shouldDownload, shouldIgnore) = UpdateDialog.ShowUpdateDialog(
                result.CurrentVersion,
                result.LatestVersion,
                result.ReleaseNotes ?? "",
                result.DownloadUrl);

            if (shouldIgnore)
            {
                // 保存忽略的版本号
                configManager?.UpdateIgnoredVersion(result.LatestVersion);
            }

            if (shouldDownload)
            {
                UpdateManager.OpenReleasePage(result.DownloadUrl);
            }
        }

        private void InitializeComponents()
        {
            // 初始化各个管理器（configManager已在构造函数中创建）
            midiManager = new MidiManager();
            layout = new MainFormLayout(this);
            trayManager = new TrayManager(
                configManager!.Config.MinimizeToTrayOnClose,
                configManager.Config.Language,
                configManager.Config.AutoCheckUpdate);
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
            trayManager.AutoBootChanged += (s, e) =>
            {
                if (configManager is null) return;
                configManager.Config.AutoBoot = e;
                configManager.SaveConfig();
                layout.LogMessage(e ? LocalizationManager.GetString("MsgAutoBootEnabled") : LocalizationManager.GetString("MsgAutoBootDisabled"));
            };
            trayManager.MinimizeToTrayOnCloseChanged += (s, e) =>
            {
                configManager?.UpdateMinimizeToTrayOnClose(e);
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
            if (layout == null) return (new List<MidiDeviceInfo>(), new List<MidiDeviceInfo>());

            // 保存当前选中的设备ID（如果已连接，使用连接的设备ID）
            int? savedInputId = null;
            int? savedOutputId = null;

            if (midiManager?.IsConnected == true)
            {
                // 如果已连接，保存当前连接的设备ID
                savedInputId = midiManager.SelectedInputDeviceId;
                savedOutputId = midiManager.SelectedOutputDeviceId;
            }
            else if (layout.InputComboBox.SelectedIndex >= 0 && layout.OutputComboBox.SelectedIndex >= 0)
            {
                // 未连接时，保存下拉框中当前选中的设备ID
                // 需要从之前的设备列表获取ID，但这里我们先用SelectedIndex作为近似
                // 实际上应该根据名称查找，因为设备ID可能会变化
                savedInputId = layout.InputComboBox.SelectedIndex;
                savedOutputId = layout.OutputComboBox.SelectedIndex;
            }

            layout.InputComboBox.Items.Clear();
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

            // 恢复选中状态
            if (midiManager?.IsConnected == true && savedInputId.HasValue && savedOutputId.HasValue)
            {
                // 已连接状态：根据设备ID找到对应的下拉框索引
                var inputIndex = inputDevices.FindIndex(d => d.Id == savedInputId.Value);
                var outputIndex = outputDevices.FindIndex(d => d.Id == savedOutputId.Value);

                if (inputIndex >= 0 && inputIndex < layout.InputComboBox.Items.Count)
                    layout.InputComboBox.SelectedIndex = inputIndex;
                else if (layout.InputComboBox.Items.Count > 0)
                    layout.InputComboBox.SelectedIndex = 0;

                if (outputIndex >= 0 && outputIndex < layout.OutputComboBox.Items.Count)
                    layout.OutputComboBox.SelectedIndex = outputIndex;
                else if (layout.OutputComboBox.Items.Count > 0)
                    layout.OutputComboBox.SelectedIndex = 0;
            }
            else if (savedInputId.HasValue && savedOutputId.HasValue)
            {
                // 未连接状态：尝试根据之前保存的索引恢复（如果设备数量足够）
                if (savedInputId.Value < layout.InputComboBox.Items.Count)
                    layout.InputComboBox.SelectedIndex = savedInputId.Value;
                else if (layout.InputComboBox.Items.Count > 0)
                    layout.InputComboBox.SelectedIndex = 0;

                if (savedOutputId.Value < layout.OutputComboBox.Items.Count)
                    layout.OutputComboBox.SelectedIndex = savedOutputId.Value;
                else if (layout.OutputComboBox.Items.Count > 0)
                    layout.OutputComboBox.SelectedIndex = 0;
            }
            else
            {
                // 默认选中第一个
                if (layout.InputComboBox.Items.Count > 0)
                    layout.InputComboBox.SelectedIndex = 0;

                if (layout.OutputComboBox.Items.Count > 0)
                    layout.OutputComboBox.SelectedIndex = 0;
            }

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
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        /// <summary>
        /// 注册自定义窗口消息处理程序，用于单实例激活
        /// </summary>
        private void RegisterShowWindowMessageHandler()
        {
            // 重写 WndProc 在构造函数中无法立即执行，因为窗口句柄还未创建
            // 所以我们订阅 HandleCreated 事件
            HandleCreated += (s, e) =>
            {
                // 窗口句柄创建完成，可以接收消息了
            };
        }

        /// <summary>
        /// 重写窗口消息处理，处理自定义的单实例激活消息
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            // 检查是否是我们注册的显示窗口消息
            if (showWindowMessage != 0 && m.Msg == showWindowMessage)
            {
                // 显示窗口（从托盘恢复）
                ShowFromTray();
                return;
            }

            base.WndProc(ref m);
        }

        private void ExitApplication()
        {
            isClosing = true;
            // 停止自动连接重试定时器
            autoConnectRetryTimer?.Stop();
            autoConnectRetryTimer?.Dispose();
            autoConnectRetryTimer = null;
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
                        configManager.UpdateMinimizeToTrayOnClose(false);
                        trayManager?.UpdateMinimizeToTrayOnCloseCheck(false);
                    }
                    else if (result == DialogResult.Yes)
                    {
                        // 用户选择最小化到托盘
                        configManager.UpdateMinimizeToTrayOnClose(true);
                        trayManager?.UpdateMinimizeToTrayOnCloseCheck(true);
                    }

                    configManager.SetTrayPromptShown();
                }

                // 根据设置决定是退出还是最小化到托盘
                if (configManager.Config.MinimizeToTrayOnClose)
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
                // 停止自动连接重试定时器
                autoConnectRetryTimer?.Stop();
                autoConnectRetryTimer?.Dispose();
                autoConnectRetryTimer = null;
                midiManager?.Dispose();
                trayManager?.Dispose();
                updateManager?.Dispose();
            }
        }
    }
}
