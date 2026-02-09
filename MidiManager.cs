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

        public bool IsConnected => midiInputDevice != null && midiOutputDevice != null;
        public int SelectedInputDeviceId => selectedInputDeviceId;
        public int SelectedOutputDeviceId => selectedOutputDeviceId;

        public event EventHandler<MidiMessageEventArgs>? MessageReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;

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

        public void Disconnect()
        {
            try
            {
                bool wasConnected = IsConnected;

                if (midiInputDevice != null)
                {
                    midiInputDevice.Stop();
                    midiInputDevice.MessageReceived -= OnMidiMessageReceived;
                    midiInputDevice.Dispose();
                    midiInputDevice = null;
                }

                if (midiOutputDevice != null)
                {
                    midiOutputDevice.Dispose();
                    midiOutputDevice = null;
                }

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
