using NAudio.Midi;

namespace MidiForwarder
{
    public class MidiDeviceInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
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

    public class MidiManager : IDisposable
    {
        private MidiIn? midiInputDevice;
        private MidiOut? midiOutputDevice;
        private int selectedInputDeviceId = -1;
        private int selectedOutputDeviceId = -1;

        // 心跳检测和自动重连
        private System.Threading.Timer? heartbeatTimer;
        private const int HeartbeatIntervalMs = 3000; // 每3秒检测一次
        private const int MaxReconnectAttempts = 5;
        private int reconnectAttemptCount = 0;
        private bool isReconnecting = false;
        private DateTime lastMessageTime = DateTime.Now;
        private const int MessageTimeoutMs = 10000; // 10秒无消息认为设备无响应

        public bool IsConnected => midiInputDevice != null && midiOutputDevice != null;
        public int SelectedInputDeviceId => selectedInputDeviceId;
        public int SelectedOutputDeviceId => selectedOutputDeviceId;

        public event EventHandler<MidiMessageEventArgs>? MessageReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        public event EventHandler? DeviceLost; // 设备丢失事件
        public event EventHandler? DeviceRestored; // 设备恢复事件

        public static List<MidiDeviceInfo> GetInputDevices()
        {
            var devices = new List<MidiDeviceInfo>();
            for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            {
                var deviceInfo = MidiIn.DeviceInfo(i);
                devices.Add(new MidiDeviceInfo { Id = i, Name = $"[{i}] {deviceInfo.ProductName}" });
            }
            return devices;
        }

        public static List<MidiDeviceInfo> GetOutputDevices()
        {
            var devices = new List<MidiDeviceInfo>();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                var deviceInfo = MidiOut.DeviceInfo(i);
                devices.Add(new MidiDeviceInfo { Id = i, Name = $"[{i}] {deviceInfo.ProductName}" });
            }
            return devices;
        }

        public bool Connect(int inputDeviceId, int outputDeviceId)
        {
            try
            {
                Disconnect();

                selectedInputDeviceId = inputDeviceId;
                selectedOutputDeviceId = outputDeviceId;

                midiInputDevice = new MidiIn(selectedInputDeviceId);
                midiInputDevice.MessageReceived += OnMidiMessageReceived;
                midiInputDevice.Start();

                midiOutputDevice = new MidiOut(selectedOutputDeviceId);

                // 重置重连计数和消息时间
                reconnectAttemptCount = 0;
                isReconnecting = false;
                lastMessageTime = DateTime.Now;

                // 启动心跳检测
                StartHeartbeatTimer();

                Connected?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"连接错误: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        private void StartHeartbeatTimer()
        {
            StopHeartbeatTimer();
            heartbeatTimer = new System.Threading.Timer(
                HeartbeatCallback,
                null,
                HeartbeatIntervalMs,
                HeartbeatIntervalMs);
        }

        private void StopHeartbeatTimer()
        {
            heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            heartbeatTimer?.Dispose();
            heartbeatTimer = null;
        }

        private void HeartbeatCallback(object? state)
        {
            try
            {
                if (!IsConnected || isReconnecting) return;

                // 检查设备是否仍然可用
                if (!CheckDeviceAvailability())
                {
                    // 设备不可用，触发丢失事件并尝试重连
                    DeviceLost?.Invoke(this, EventArgs.Empty);
                    _ = Task.Run(TryReconnectAsync);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"心跳检测错误: {ex.Message}");
            }
        }

        private bool CheckDeviceAvailability()
        {
            try
            {
                // 检查输入设备是否仍然存在于设备列表中
                bool inputDeviceExists = false;
                bool outputDeviceExists = false;

                for (int i = 0; i < MidiIn.NumberOfDevices; i++)
                {
                    if (i == selectedInputDeviceId)
                    {
                        inputDeviceExists = true;
                        break;
                    }
                }

                for (int i = 0; i < MidiOut.NumberOfDevices; i++)
                {
                    if (i == selectedOutputDeviceId)
                    {
                        outputDeviceExists = true;
                        break;
                    }
                }

                // 如果设备不存在于列表中，说明设备已断开
                if (!inputDeviceExists || !outputDeviceExists)
                {
                    return false;
                }

                // 检查是否长时间没有收到消息（可能设备无响应）
                var timeSinceLastMessage = DateTime.Now - lastMessageTime;
                if (timeSinceLastMessage.TotalMilliseconds > MessageTimeoutMs)
                {
                    // 尝试发送一个测试消息来验证设备是否响应
                    try
                    {
                        midiOutputDevice?.Send(0xFE); // 发送 Active Sensing 消息
                    }
                    catch
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task TryReconnectAsync()
        {
            if (isReconnecting) return;
            isReconnecting = true;

            try
            {
                // 先断开当前连接
                await Task.Run(() => DisconnectInternal());

                // 等待设备重新出现
                bool deviceRestored = false;
                for (int attempt = 0; attempt < MaxReconnectAttempts; attempt++)
                {
                    await Task.Delay(1000); // 等待1秒

                    // 检查设备是否重新出现
                    bool inputDeviceExists = false;
                    bool outputDeviceExists = false;

                    for (int i = 0; i < MidiIn.NumberOfDevices; i++)
                    {
                        if (i == selectedInputDeviceId)
                        {
                            inputDeviceExists = true;
                            break;
                        }
                    }

                    for (int i = 0; i < MidiOut.NumberOfDevices; i++)
                    {
                        if (i == selectedOutputDeviceId)
                        {
                            outputDeviceExists = true;
                            break;
                        }
                    }

                    if (inputDeviceExists && outputDeviceExists)
                    {
                        deviceRestored = true;
                        break;
                    }
                }

                if (deviceRestored)
                {
                    // 尝试重新连接
                    if (Connect(selectedInputDeviceId, selectedOutputDeviceId))
                    {
                        reconnectAttemptCount = 0;
                        DeviceRestored?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        reconnectAttemptCount++;
                        if (reconnectAttemptCount >= MaxReconnectAttempts)
                        {
                            ErrorOccurred?.Invoke(this, "设备重连失败，已达到最大重试次数");
                        }
                    }
                }
                else
                {
                    ErrorOccurred?.Invoke(this, "设备未重新出现，请检查设备连接");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"重连错误: {ex.Message}");
            }
            finally
            {
                isReconnecting = false;
            }
        }

        private void DisconnectInternal()
        {
            try
            {
                if (midiInputDevice != null)
                {
                    try
                    {
                        midiInputDevice.Stop();
                    }
                    catch { }
                    midiInputDevice.MessageReceived -= OnMidiMessageReceived;
                    midiInputDevice.Dispose();
                    midiInputDevice = null;
                }

                if (midiOutputDevice != null)
                {
                    try
                    {
                        midiOutputDevice.Dispose();
                    }
                    catch { }
                    midiOutputDevice = null;
                }
            }
            catch { }
        }

        public void Disconnect()
        {
            try
            {
                bool wasConnected = IsConnected;

                // 停止心跳检测
                StopHeartbeatTimer();

                DisconnectInternal();

                if (wasConnected)
                {
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"断开连接错误: {ex.Message}");
            }
        }

        private void OnMidiMessageReceived(object? sender, MidiInMessageEventArgs e)
        {
            try
            {
                // 更新最后消息时间
                lastMessageTime = DateTime.Now;

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
            StopHeartbeatTimer();
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}
