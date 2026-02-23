using NAudio.Midi;

namespace MidiForwarder
{
    public class Midi10Manager : IMidiManager
    {
        private MidiIn? midiInputDevice;
        private MidiOut? midiOutputDevice;
        private int selectedInputDeviceId = -1;
        private int selectedOutputDeviceId = -1;
        private readonly ConfigManager? configManager;

        public bool IsConnected => midiInputDevice != null && midiOutputDevice != null;
        public string SelectedInputDeviceId => selectedInputDeviceId.ToString();
        public string SelectedOutputDeviceId => selectedOutputDeviceId.ToString();

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

        public Midi10Manager(ConfigManager? configManager = null)
        {
            this.configManager = configManager;
            Logger.Info("Midi10Manager 已创建 (MIDI 1.0 API)");
        }

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
            if (IsBluetoothBlePattern(deviceName))
            {
                return true;
            }

            // 检查 "bt" 模式 - 需要是独立的词或带有连字符
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

        /// <summary>
        /// 检查是否为蓝牙BT模式
        /// </summary>
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
                    Id = i.ToString(),
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
                    Id = i.ToString(),
                    Name = name,
                    IsBluetooth = IsBluetoothDevice(deviceInfo.ProductName, exclusions)
                });
            }
            return devices;
        }

        public bool Connect(string inputDeviceId, string outputDeviceId)
        {
            if (!int.TryParse(inputDeviceId, out int inputId) || !int.TryParse(outputDeviceId, out int outputId))
            {
                Logger.Error("无效的设备ID格式");
                return false;
            }

            Logger.Info($"Midi10Manager.Connect: 开始连接设备 - 输入ID={inputId}, 输出ID={outputId}");

            try
            {
                Logger.Info("Midi10Manager.Connect: 断开现有连接");
                Disconnect();

                selectedInputDeviceId = inputId;
                selectedOutputDeviceId = outputId;

                string inputDeviceName = "Unknown";
                string outputDeviceName = "Unknown";
                bool isInputBluetooth = false;
                bool isOutputBluetooth = false;

                var exclusions = configManager?.GetWiredDeviceExclusions();

                try
                {
                    if (inputId >= 0 && inputId < MidiIn.NumberOfDevices)
                    {
                        inputDeviceName = MidiIn.DeviceInfo(inputId).ProductName;
                        isInputBluetooth = IsBluetoothDevice(inputDeviceName, exclusions);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info($"Midi10Manager.Connect: 获取输入设备名称失败 - {ex.Message}");
                }

                try
                {
                    if (outputId >= 0 && outputId < MidiOut.NumberOfDevices)
                    {
                        outputDeviceName = MidiOut.DeviceInfo(outputId).ProductName;
                        isOutputBluetooth = IsBluetoothDevice(outputDeviceName, exclusions);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info($"Midi10Manager.Connect: 获取输出设备名称失败 - {ex.Message}");
                }

                Logger.Info($"Midi10Manager.Connect: 输入设备='{inputDeviceName}', 输出设备='{outputDeviceName}'");

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

                Logger.Info("Midi10Manager.Connect: 创建MidiIn实例");
                midiInputDevice = new MidiIn(selectedInputDeviceId);
                midiInputDevice.MessageReceived += OnMidiMessageReceived;

                Logger.Info("Midi10Manager.Connect: 启动MidiIn");
                midiInputDevice.Start();

                Logger.Info("Midi10Manager.Connect: 创建MidiOut实例");
                midiOutputDevice = new MidiOut(selectedOutputDeviceId);

                Logger.Info("Midi10Manager.Connect: 连接成功");
                Connected?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Midi10Manager.Connect: 连接失败 - {ex.Message}", ex);
                ErrorOccurred?.Invoke(this, $"连接错误: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            Logger.Info($"Midi10Manager.Disconnect: 开始断开连接 - 当前连接状态={IsConnected}");
            try
            {
                bool wasConnected = IsConnected;

                if (midiInputDevice != null)
                {
                    Logger.Info("Midi10Manager.Disconnect: 停止MidiIn");
                    try
                    {
                        midiInputDevice.Stop();
                    }
                    catch { }
                    midiInputDevice.MessageReceived -= OnMidiMessageReceived;
                    midiInputDevice.Dispose();
                    midiInputDevice = null;
                    Logger.Info("Midi10Manager.Disconnect: MidiIn已释放");
                }

                if (midiOutputDevice != null)
                {
                    Logger.Info("Midi10Manager.Disconnect: 释放MidiOut");
                    try
                    {
                        midiOutputDevice.Dispose();
                    }
                    catch { }
                    midiOutputDevice = null;
                    Logger.Info("Midi10Manager.Disconnect: MidiOut已释放");
                }

                if (wasConnected)
                {
                    Logger.Info("Midi10Manager.Disconnect: 触发Disconnected事件");
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }

                Logger.Info("Midi10Manager.Disconnect: 断开连接完成");
            }
            catch (Exception ex)
            {
                Logger.Error($"Midi10Manager.Disconnect: 断开连接错误 - {ex.Message}", ex);
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
