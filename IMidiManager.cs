namespace MidiForwarder
{
    public interface IMidiManager : IDisposable
    {
        bool IsConnected { get; }
        string SelectedInputDeviceId { get; }
        string SelectedOutputDeviceId { get; }

        event EventHandler<MidiMessageEventArgs>? MessageReceived;
        event EventHandler<string>? ErrorOccurred;
        event EventHandler? Connected;
        event EventHandler? Disconnected;

        List<MidiDeviceInfo> GetInputDevices();
        List<MidiDeviceInfo> GetOutputDevices();
        bool Connect(string inputDeviceId, string outputDeviceId);
        void Disconnect();
    }

    public enum MidiApiType
    {
        Midi10,  // NAudio + WinMM
        Midi20   // Windows MIDI Services
    }

    public static class MidiApiFactory
    {
        public static bool IsMidi20Available { get; private set; }

        static MidiApiFactory()
        {
            CheckMidi20Availability();
        }

        private static void CheckMidi20Availability()
        {
            try
            {
#if WINDOWS_MIDI_SERVICES
                // 检查 Windows MIDI Services 是否安装
                var serviceAvailable = Microsoft.Windows.Devices.Midi2.MidiServices.IsAvailable();
                IsMidi20Available = serviceAvailable;
                Logger.Info($"Windows MIDI Services 可用性检查结果: {serviceAvailable}");
#else
                IsMidi20Available = false;
                Logger.Info("Windows MIDI Services SDK 未引用，使用 MIDI 1.0");
#endif
            }
            catch (Exception ex)
            {
                IsMidi20Available = false;
                Logger.Info($"Windows MIDI Services 检查失败: {ex.Message}");
            }
        }

        public static IMidiManager CreateMidiManager(ConfigManager? configManager = null, MidiApiType? preferredApi = null)
        {
            var apiType = preferredApi ?? (IsMidi20Available ? MidiApiType.Midi20 : MidiApiType.Midi10);

            Logger.Info($"创建 MidiManager，首选 API: {apiType}");

            if (apiType == MidiApiType.Midi20 && IsMidi20Available)
            {
#if WINDOWS_MIDI_SERVICES
                try
                {
                    return new Midi20Manager(configManager);
                }
                catch (Exception ex)
                {
                    Logger.Error($"创建 MIDI 2.0 Manager 失败，回退到 MIDI 1.0: {ex.Message}");
                }
#endif
            }

            return new Midi10Manager(configManager);
        }

        public static IMidiManager CreateMidiManagerWithFallback(ConfigManager? configManager = null)
        {
            return CreateMidiManager(configManager, null);
        }
    }
}
