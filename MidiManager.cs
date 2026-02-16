using NAudio.Midi;

namespace MidiForwarder
{
    public class MidiDeviceInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsBluetooth { get; set; }
    }

    public class MidiMessageEventArgs : EventArgs
    {
        public int RawMessage { get; set; }
        public string MessageType { get; set; } = "";
        public int Channel { get; set; }
        public int Data1 { get; set; }
        public int Data2 { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class MidiManager(ConfigManager? configManager = null) : IDisposable
    {
        private MidiIn? midiInputDevice;
        private MidiOut? midiOutputDevice;
        private int selectedInputDeviceId = -1;
        private int selectedOutputDeviceId = -1;
        private readonly ConfigManager? configManager = configManager;

        public bool IsConnected => midiInputDevice != null && midiOutputDevice != null;
        public int SelectedInputDeviceId => selectedInputDeviceId;
        public int SelectedOutputDeviceId => selectedOutputDeviceId;

        public event EventHandler<MidiMessageEventArgs>? MessageReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;

        // 蓝牙设备关键词列表 - 这些词明确表明是蓝牙设备
        private static readonly string[] BluetoothKeywords =
        [
            "bluetooth", "无线", "wireless",
            "蓝牙", "btooth", "btmidi", "midi bt"
        ];

        /// <summary>
        /// 检查设备是否为蓝牙设备
        /// </summary>
        public static bool IsBluetoothDevice(string deviceName, List<string>? exclusions = null)
        {
            if (string.IsNullOrEmpty(deviceName)) return false;

            // 先检查是否在排除名单中
            if (exclusions != null && exclusions.Any(exclusion =>
                deviceName.Contains(exclusion, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // 检查明确的蓝牙关键词
            if (BluetoothKeywords.Any(keyword =>
                deviceName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // 检查 "ble" 模式 - 需要是独立的词或带有连字符
            // 例如: "BLE MIDI", "BLE-MIDI", "MIDI BLE", "MIDI-BLE" 是蓝牙
            // 但 "Wavetable", "Cable" 中的 "ble" 不是
            if (IsBluetoothBlePattern(deviceName))
            {
                return true;
            }

            // 检查 "bt" 模式 - 需要是独立的词或带有连字符
            // 例如: "BT MIDI", "BT-MIDI", "MIDI BT", "MIDI-BT" 是蓝牙
            if (IsBluetoothBtPattern(deviceName))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查是否为蓝牙BLE模式（避免匹配到Wavetable等词中的ble）
        /// </summary>
        private static bool IsBluetoothBlePattern(string deviceName)
        {
            // 查找 "ble" 的位置（不区分大小写）
            int index = deviceName.IndexOf("ble", StringComparison.OrdinalIgnoreCase);
            while (index != -1)
            {
                // 检查 "ble" 前后是否有蓝牙特征
                // 前导字符：空格、连字符、字符串开始
                // 后随字符：空格、连字符、字符串结束、数字
                bool validPrefix = index == 0 ||
                    deviceName[index - 1] == ' ' ||
                    deviceName[index - 1] == '-';

                bool validSuffix = index + 3 >= deviceName.Length ||
                    deviceName[index + 3] == ' ' ||
                    deviceName[index + 3] == '-' ||
                    char.IsDigit(deviceName[index + 3]);

                // 如果前后都是有效的分隔符，则是蓝牙BLE
                if (validPrefix && validSuffix)
                {
                    return true;
                }

                // 继续查找下一个 "ble"
                index = deviceName.IndexOf("ble", index + 1, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// 检查是否为蓝牙BT模式
        /// </summary>
        private static bool IsBluetoothBtPattern(string deviceName)
        {
            // 查找 " bt" 或 "bt " 或 "-bt" 或 "bt-" 模式（不区分大小写）
            string[] btPatterns = [" bt ", " bt-", "-bt ", "-bt-", " bt", "bt "];
            foreach (var pattern in btPatterns)
            {
                if (deviceName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 检查字符串开头或结尾的 "bt"（不区分大小写）
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
        /// 获取输入设备列表（使用配置的排除名单）
        /// </summary>
        public List<MidiDeviceInfo> GetInputDevices()
        {
            var exclusions = configManager?.GetWiredDeviceExclusions();
            return GetInputDevices(exclusions);
        }

        /// <summary>
        /// 获取输入设备列表（静态版本，可传入排除名单）
        /// </summary>
        public static List<MidiDeviceInfo> GetInputDevices(List<string>? exclusions = null)
        {
            var devices = new List<MidiDeviceInfo>();
            for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            {
                var deviceInfo = MidiIn.DeviceInfo(i);
                var name = $"[{i}] {deviceInfo.ProductName}";
                devices.Add(new MidiDeviceInfo
                {
                    Id = i,
                    Name = name,
                    IsBluetooth = IsBluetoothDevice(deviceInfo.ProductName, exclusions)
                });
            }
            return devices;
        }

        /// <summary>
        /// 获取输出设备列表（使用配置的排除名单）
        /// </summary>
        public List<MidiDeviceInfo> GetOutputDevices()
        {
            var exclusions = configManager?.GetWiredDeviceExclusions();
            return GetOutputDevices(exclusions);
        }

        /// <summary>
        /// 获取输出设备列表（静态版本，可传入排除名单）
        /// </summary>
        public static List<MidiDeviceInfo> GetOutputDevices(List<string>? exclusions = null)
        {
            var devices = new List<MidiDeviceInfo>();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                var deviceInfo = MidiOut.DeviceInfo(i);
                var name = $"[{i}] {deviceInfo.ProductName}";
                devices.Add(new MidiDeviceInfo
                {
                    Id = i,
                    Name = name,
                    IsBluetooth = IsBluetoothDevice(deviceInfo.ProductName, exclusions)
                });
            }
            return devices;
        }

        public bool Connect(int inputDeviceId, int outputDeviceId)
        {
            Logger.Info($"MidiManager.Connect: 开始连接设备 - 输入ID={inputDeviceId}, 输出ID={outputDeviceId}");

            try
            {
                Logger.Info("MidiManager.Connect: 断开现有连接");
                Disconnect();

                selectedInputDeviceId = inputDeviceId;
                selectedOutputDeviceId = outputDeviceId;

                // 获取设备名称用于日志
                string inputDeviceName = "Unknown";
                string outputDeviceName = "Unknown";
                bool isInputBluetooth = false;
                bool isOutputBluetooth = false;

                var exclusions = configManager?.GetWiredDeviceExclusions();

                try
                {
                    if (inputDeviceId >= 0 && inputDeviceId < MidiIn.NumberOfDevices)
                    {
                        inputDeviceName = MidiIn.DeviceInfo(inputDeviceId).ProductName;
                        isInputBluetooth = IsBluetoothDevice(inputDeviceName, exclusions);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info($"MidiManager.Connect: 获取输入设备名称失败 - {ex.Message}");
                }

                try
                {
                    if (outputDeviceId >= 0 && outputDeviceId < MidiOut.NumberOfDevices)
                    {
                        outputDeviceName = MidiOut.DeviceInfo(outputDeviceId).ProductName;
                        isOutputBluetooth = IsBluetoothDevice(outputDeviceName, exclusions);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info($"MidiManager.Connect: 获取输出设备名称失败 - {ex.Message}");
                }

                Logger.Info($"MidiManager.Connect: 输入设备='{inputDeviceName}', 输出设备='{outputDeviceName}'");

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

                Logger.Info("MidiManager.Connect: 创建MidiIn实例");
                midiInputDevice = new MidiIn(selectedInputDeviceId);
                midiInputDevice.MessageReceived += OnMidiMessageReceived;

                Logger.Info("MidiManager.Connect: 启动MidiIn");
                midiInputDevice.Start();

                Logger.Info("MidiManager.Connect: 创建MidiOut实例");
                midiOutputDevice = new MidiOut(selectedOutputDeviceId);

                Logger.Info("MidiManager.Connect: 连接成功");
                Connected?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"MidiManager.Connect: 连接失败 - {ex.Message}", ex);
                ErrorOccurred?.Invoke(this, $"连接错误: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            Logger.Info($"MidiManager.Disconnect: 开始断开连接 - 当前连接状态={IsConnected}");
            try
            {
                bool wasConnected = IsConnected;

                if (midiInputDevice != null)
                {
                    Logger.Info("MidiManager.Disconnect: 停止MidiIn");
                    try
                    {
                        midiInputDevice.Stop();
                    }
                    catch { }
                    midiInputDevice.MessageReceived -= OnMidiMessageReceived;
                    midiInputDevice.Dispose();
                    midiInputDevice = null;
                    Logger.Info("MidiManager.Disconnect: MidiIn已释放");
                }

                if (midiOutputDevice != null)
                {
                    Logger.Info("MidiManager.Disconnect: 释放MidiOut");
                    try
                    {
                        midiOutputDevice.Dispose();
                    }
                    catch { }
                    midiOutputDevice = null;
                    Logger.Info("MidiManager.Disconnect: MidiOut已释放");
                }

                if (wasConnected)
                {
                    Logger.Info("MidiManager.Disconnect: 触发Disconnected事件");
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }

                Logger.Info("MidiManager.Disconnect: 断开连接完成");
            }
            catch (Exception ex)
            {
                Logger.Error($"MidiManager.Disconnect: 断开连接错误 - {ex.Message}", ex);
                ErrorOccurred?.Invoke(this, $"断开连接错误: {ex.Message}");
            }
        }

        private void OnMidiMessageReceived(object? sender, MidiInMessageEventArgs e)
        {
            try
            {
                midiOutputDevice?.Send(e.RawMessage);

                var messageArgs = ParseMidiMessage(e.RawMessage);
                MessageReceived?.Invoke(this, messageArgs);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"转发错误: {ex.Message}");
            }
        }

        private static MidiMessageEventArgs ParseMidiMessage(int rawMessage)
        {
            var statusByte = rawMessage & 0xFF;
            var command = statusByte & 0xF0;
            var channel = (statusByte & 0x0F) + 1;

            string messageType = command switch
            {
                0x80 => "Note Off",
                0x90 => "Note On",
                0xA0 => "Aftertouch",
                0xB0 => "Control Change",
                0xC0 => "Program Change",
                0xD0 => "Channel Pressure",
                0xE0 => "Pitch Bend",
                _ => "Unknown"
            };

            return new MidiMessageEventArgs
            {
                RawMessage = rawMessage,
                MessageType = messageType,
                Channel = channel,
                Data1 = (rawMessage >> 8) & 0x7F,
                Data2 = (rawMessage >> 16) & 0x7F,
                Timestamp = DateTime.Now
            };
        }

        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}
