#if WINDOWS_MIDI_SERVICES
using Microsoft.Windows.Devices.Midi2;
using Microsoft.Windows.Devices.Midi2.Messages;
#endif

namespace MidiForwarder
{
    public class Midi20Manager : IMidiManager
    {
#if WINDOWS_MIDI_SERVICES
        private MidiSession? midiSession;
        private MidiEndpointConnection? inputConnection;
        private MidiEndpointConnection? outputConnection;
        private string selectedInputEndpointId = "";
        private string selectedOutputEndpointId = "";
        private readonly ConfigManager? configManager;

        public bool IsConnected => inputConnection != null && outputConnection != null &&
                                   inputConnection.IsOpen && outputConnection.IsOpen;
        public string SelectedInputDeviceId => selectedInputEndpointId;
        public string SelectedOutputDeviceId => selectedOutputEndpointId;

        public event EventHandler<MidiMessageEventArgs>? MessageReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;

        public Midi20Manager(ConfigManager? configManager = null)
        {
            this.configManager = configManager;
            Logger.Info("Midi20Manager 已创建 (Windows MIDI Services 2.0 API)");
            InitializeSession();
        }

        private void InitializeSession()
        {
            try
            {
                if (!MidiServices.IsAvailable())
                {
                    throw new InvalidOperationException("Windows MIDI Services 不可用");
                }

                midiSession = MidiSession.Create("MidiForwarder Session");
                Logger.Info("Midi20Manager: MIDI 会话已创建");
            }
            catch (Exception ex)
            {
                Logger.Error($"Midi20Manager: 初始化 MIDI 会话失败 - {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 检查设备是否为蓝牙设备
        /// </summary>
        private static bool IsBluetoothDevice(string deviceName, List<string>? exclusions = null)
        {
            if (string.IsNullOrEmpty(deviceName)) return false;

            // 先检查是否在排除名单中
            if (exclusions != null && exclusions.Any(exclusion =>
                deviceName.Contains(exclusion, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // 检查明确的蓝牙关键词
            string[] bluetoothKeywords = ["bluetooth", "无线", "wireless", "蓝牙", "btooth", "btmidi", "midi bt"];
            if (bluetoothKeywords.Any(keyword =>
                deviceName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // 检查 "ble" 模式
            if (IsBluetoothBlePattern(deviceName))
            {
                return true;
            }

            // 检查 "bt" 模式
            if (IsBluetoothBtPattern(deviceName))
            {
                return true;
            }

            return false;
        }

        private static bool IsBluetoothBlePattern(string deviceName)
        {
            int index = deviceName.IndexOf("ble", StringComparison.OrdinalIgnoreCase);
            while (index != -1)
            {
                bool validPrefix = index == 0 ||
                    deviceName[index - 1] == ' ' ||
                    deviceName[index - 1] == '-';

                bool validSuffix = index + 3 >= deviceName.Length ||
                    deviceName[index + 3] == ' ' ||
                    deviceName[index + 3] == '-' ||
                    char.IsDigit(deviceName[index + 3]);

                if (validPrefix && validSuffix)
                {
                    return true;
                }

                index = deviceName.IndexOf("ble", index + 1, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsBluetoothBtPattern(string deviceName)
        {
            string[] btPatterns = [" bt ", " bt-", "-bt ", "-bt-", " bt", "bt "];
            foreach (var pattern in btPatterns)
            {
                if (deviceName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (deviceName.StartsWith("bt ", StringComparison.OrdinalIgnoreCase) ||
                deviceName.StartsWith("bt-", StringComparison.OrdinalIgnoreCase) ||
                deviceName.EndsWith(" bt", StringComparison.OrdinalIgnoreCase) ||
                deviceName.EndsWith("-bt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取输入设备列表
        /// </summary>
        public List<MidiDeviceInfo> GetInputDevices()
        {
            var exclusions = configManager?.GetWiredDeviceExclusions();
            return GetInputDevices(exclusions);
        }

        /// <summary>
        /// 获取输入设备列表（静态版本）
        /// </summary>
        public static List<MidiDeviceInfo> GetInputDevices(List<string>? exclusions = null)
        {
            var devices = new List<MidiDeviceInfo>();

            try
            {
                var endpointInfos = MidiEndpointDeviceInformation.FindAll(
                    MidiEndpointDeviceInformationSortOrder.Name,
                    MidiEndpointDeviceInformationFilter.IncludeClientByteStreamEndpoints |
                    MidiEndpointDeviceInformationFilter.IncludeClientUmpEndpoints);

                foreach (var endpointInfo in endpointInfos)
                {
                    // 只包含输入端点
                    if (endpointInfo.EndpointPurpose == MidiEndpointDeviceInformationEndpointPurpose.Normal &&
                        (endpointInfo.NativeDataFormat == MidiEndpointNativeDataFormat.ByteStream ||
                         endpointInfo.NativeDataFormat == MidiEndpointNativeDataFormat.UniversalMidiPacket))
                    {
                        var name = endpointInfo.Name;
                        var endpointId = endpointInfo.EndpointDeviceId;

                        devices.Add(new MidiDeviceInfo
                        {
                            Id = endpointId,
                            Name = name,
                            IsBluetooth = IsBluetoothDevice(name, exclusions),
                            IsMidi20 = endpointInfo.NativeDataFormat == MidiEndpointNativeDataFormat.UniversalMidiPacket,
                            EndpointId = endpointId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取 MIDI 2.0 输入设备失败: {ex.Message}", ex);
            }

            return devices;
        }

        /// <summary>
        /// 获取输出设备列表
        /// </summary>
        public List<MidiDeviceInfo> GetOutputDevices()
        {
            var exclusions = configManager?.GetWiredDeviceExclusions();
            return GetOutputDevices(exclusions);
        }

        /// <summary>
        /// 获取输出设备列表（静态版本）
        /// </summary>
        public static List<MidiDeviceInfo> GetOutputDevices(List<string>? exclusions = null)
        {
            var devices = new List<MidiDeviceInfo>();

            try
            {
                var endpointInfos = MidiEndpointDeviceInformation.FindAll(
                    MidiEndpointDeviceInformationSortOrder.Name,
                    MidiEndpointDeviceInformationFilter.IncludeClientByteStreamEndpoints |
                    MidiEndpointDeviceInformationFilter.IncludeClientUmpEndpoints);

                foreach (var endpointInfo in endpointInfos)
                {
                    // 只包含输出端点
                    if (endpointInfo.EndpointPurpose == MidiEndpointDeviceInformationEndpointPurpose.Normal &&
                        (endpointInfo.NativeDataFormat == MidiEndpointNativeDataFormat.ByteStream ||
                         endpointInfo.NativeDataFormat == MidiEndpointNativeDataFormat.UniversalMidiPacket))
                    {
                        var name = endpointInfo.Name;
                        var endpointId = endpointInfo.EndpointDeviceId;

                        devices.Add(new MidiDeviceInfo
                        {
                            Id = endpointId,
                            Name = name,
                            IsBluetooth = IsBluetoothDevice(name, exclusions),
                            IsMidi20 = endpointInfo.NativeDataFormat == MidiEndpointNativeDataFormat.UniversalMidiPacket,
                            EndpointId = endpointId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取 MIDI 2.0 输出设备失败: {ex.Message}", ex);
            }

            return devices;
        }

        public bool Connect(string inputDeviceId, string outputDeviceId)
        {
            Logger.Info($"Midi20Manager.Connect: 开始连接设备 - 输入ID={inputDeviceId}, 输出ID={outputDeviceId}");

            try
            {
                if (midiSession == null)
                {
                    Logger.Error("MIDI 会话未初始化");
                    return false;
                }

                Disconnect();

                selectedInputEndpointId = inputDeviceId;
                selectedOutputEndpointId = outputDeviceId;

                // 获取设备信息
                string inputDeviceName = "Unknown";
                string outputDeviceName = "Unknown";
                bool isInputBluetooth = false;
                bool isOutputBluetooth = false;
                bool isInputMidi20 = false;
                bool isOutputMidi20 = false;

                var exclusions = configManager?.GetWiredDeviceExclusions();

                try
                {
                    var inputInfo = MidiEndpointDeviceInformation.CreateFromEndpointDeviceId(inputDeviceId);
                    inputDeviceName = inputInfo.Name;
                    isInputBluetooth = IsBluetoothDevice(inputDeviceName, exclusions);
                    isInputMidi20 = inputInfo.NativeDataFormat == MidiEndpointNativeDataFormat.UniversalMidiPacket;
                }
                catch (Exception ex)
                {
                    Logger.Info($"Midi20Manager.Connect: 获取输入设备信息失败 - {ex.Message}");
                }

                try
                {
                    var outputInfo = MidiEndpointDeviceInformation.CreateFromEndpointDeviceId(outputDeviceId);
                    outputDeviceName = outputInfo.Name;
                    isOutputBluetooth = IsBluetoothDevice(outputDeviceName, exclusions);
                    isOutputMidi20 = outputInfo.NativeDataFormat == MidiEndpointNativeDataFormat.UniversalMidiPacket;
                }
                catch (Exception ex)
                {
                    Logger.Info($"Midi20Manager.Connect: 获取输出设备信息失败 - {ex.Message}");
                }

                Logger.Info($"Midi20Manager.Connect: 输入设备='{inputDeviceName}' (MIDI 2.0: {isInputMidi20}), 输出设备='{outputDeviceName}' (MIDI 2.0: {isOutputMidi20})");

                // 蓝牙设备警告提示
                if (isInputBluetooth || isOutputBluetooth)
                {
                    AppLog.Info("=== " + LocalizationManager.GetString("LogBluetoothMidiDetected") + " ===");
                    AppLog.Info(LocalizationManager.GetString("LogBluetoothNote"));
                    AppLog.Info(LocalizationManager.GetString("LogBluetoothTryIfFail"));
                    AppLog.Info("  " + LocalizationManager.GetString("LogBluetoothTry1"));
                    AppLog.Info("  " + LocalizationManager.GetString("LogBluetoothTry2"));
                    AppLog.Info("  " + LocalizationManager.GetString("LogBluetoothTry3"));
                    if (isInputBluetooth)
                        AppLog.Info(string.Format(LocalizationManager.GetString("LogDetectedBluetoothInput"), inputDeviceName));
                    if (isOutputBluetooth)
                        AppLog.Info(string.Format(LocalizationManager.GetString("LogDetectedBluetoothOutput"), outputDeviceName));
                }

                // 创建输入连接
                Logger.Info("Midi20Manager.Connect: 创建输入连接");
                inputConnection = midiSession.CreateEndpointConnection(inputDeviceId);
                if (inputConnection == null)
                {
                    Logger.Error("无法创建输入连接");
                    return false;
                }

                inputConnection.MessageReceived += OnMidiMessageReceived;

                // 打开输入连接
                var openResult = inputConnection.Open();
                if (openResult != MidiEndpointConnectionOpenResult.Success)
                {
                    Logger.Error($"打开输入连接失败: {openResult}");
                    return false;
                }

                // 创建输出连接
                Logger.Info("Midi20Manager.Connect: 创建输出连接");
                outputConnection = midiSession.CreateEndpointConnection(outputDeviceId);
                if (outputConnection == null)
                {
                    Logger.Error("无法创建输出连接");
                    inputConnection.Close();
                    return false;
                }

                // 打开输出连接
                openResult = outputConnection.Open();
                if (openResult != MidiEndpointConnectionOpenResult.Success)
                {
                    Logger.Error($"打开输出连接失败: {openResult}");
                    inputConnection.Close();
                    return false;
                }

                Logger.Info("Midi20Manager.Connect: 连接成功");
                Connected?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Midi20Manager.Connect: 连接失败 - {ex.Message}", ex);
                ErrorOccurred?.Invoke(this, $"连接错误: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            Logger.Info($"Midi20Manager.Disconnect: 开始断开连接 - 当前连接状态={IsConnected}");
            try
            {
                bool wasConnected = IsConnected;

                if (inputConnection != null)
                {
                    Logger.Info("Midi20Manager.Disconnect: 关闭输入连接");
                    try
                    {
                        inputConnection.MessageReceived -= OnMidiMessageReceived;
                        inputConnection.Close();
                    }
                    catch { }
                    inputConnection = null;
                    Logger.Info("Midi20Manager.Disconnect: 输入连接已释放");
                }

                if (outputConnection != null)
                {
                    Logger.Info("Midi20Manager.Disconnect: 关闭输出连接");
                    try
                    {
                        outputConnection.Close();
                    }
                    catch { }
                    outputConnection = null;
                    Logger.Info("Midi20Manager.Disconnect: 输出连接已释放");
                }

                if (wasConnected)
                {
                    Logger.Info("Midi20Manager.Disconnect: 触发Disconnected事件");
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }

                Logger.Info("Midi20Manager.Disconnect: 断开连接完成");
            }
            catch (Exception ex)
            {
                Logger.Error($"Midi20Manager.Disconnect: 断开连接错误 - {ex.Message}", ex);
                ErrorOccurred?.Invoke(this, $"断开连接错误: {ex.Message}");
            }
        }

        private void OnMidiMessageReceived(IMidiMessageReceivedEventSource sender, MidiMessageReceivedEventArgs args)
        {
            try
            {
                var message = args.Message;

                // 转发消息到输出设备
                if (outputConnection != null && outputConnection.IsOpen)
                {
                    outputConnection.SendMessage(message);
                }

                // 解析并触发事件
                var messageArgs = ParseMidiMessage(message);
                MessageReceived?.Invoke(this, messageArgs);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"转发错误: {ex.Message}");
            }
        }

        private static MidiMessageEventArgs ParseMidiMessage(IMidiMessage message)
        {
            var args = new MidiMessageEventArgs
            {
                Timestamp = DateTime.Now
            };

            try
            {
                // 获取消息类型
                if (message is MidiMessage32 message32)
                {
                    var word = message32.Word0;
                    var messageType = (word >> 28) & 0xF;
                    var group = (word >> 24) & 0xF;
                    var status = (word >> 16) & 0xFF;
                    var data1 = (word >> 8) & 0xFF;
                    var data2 = word & 0xFF;

                    // 转换为 MIDI 1.0 格式用于显示
                    var command = status & 0xF0;
                    var channel = (status & 0x0F) + 1;

                    args.RawMessage = (data2 << 16) | (data1 << 8) | status;
                    args.Channel = channel;
                    args.Data1 = data1 & 0x7F;
                    args.Data2 = data2 & 0x7F;

                    args.MessageType = command switch
                    {
                        0x80 => "Note Off",
                        0x90 => "Note On",
                        0xA0 => "Aftertouch",
                        0xB0 => "Control Change",
                        0xC0 => "Program Change",
                        0xD0 => "Channel Pressure",
                        0xE0 => "Pitch Bend",
                        _ => $"UMP Type {messageType}"
                    };
                }
                else if (message is MidiMessage64 message64)
                {
                    args.RawMessage = (int)(message64.Word0 & 0xFFFFFFFF);
                    args.MessageType = "MIDI 2.0 (64-bit)";
                }
                else if (message is MidiMessage96 message96)
                {
                    args.RawMessage = (int)(message96.Word0 & 0xFFFFFFFF);
                    args.MessageType = "MIDI 2.0 (96-bit)";
                }
                else if (message is MidiMessage128 message128)
                {
                    args.RawMessage = (int)(message128.Word0 & 0xFFFFFFFF);
                    args.MessageType = "MIDI 2.0 (128-bit)";
                }
                else
                {
                    args.MessageType = "Unknown";
                }
            }
            catch
            {
                args.MessageType = "Parse Error";
            }

            return args;
        }

        public void Dispose()
        {
            Disconnect();

            if (midiSession != null)
            {
                try
                {
                    midiSession.Dispose();
                    midiSession = null;
                }
                catch { }
            }

            GC.SuppressFinalize(this);
        }
#else
        // 当 WINDOWS_MIDI_SERVICES 未定义时的占位实现
        public bool IsConnected => false;
        public string SelectedInputDeviceId => "";
        public string SelectedOutputDeviceId => "";

        public event EventHandler<MidiMessageEventArgs>? MessageReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;

        public Midi20Manager(ConfigManager? configManager = null)
        {
            throw new NotSupportedException("Windows MIDI Services SDK 未启用。请确保已安装 SDK 并在项目文件中定义了 WINDOWS_MIDI_SERVICES 常量。");
        }

        public List<MidiDeviceInfo> GetInputDevices() => [];
        public List<MidiDeviceInfo> GetOutputDevices() => [];
        public bool Connect(string inputDeviceId, string outputDeviceId) => false;
        public void Disconnect() { }
        public void Dispose() { }
#endif
    }
}
