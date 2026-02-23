namespace MidiForwarder
{
    public class MidiDeviceInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsBluetooth { get; set; }
        public bool IsMidi20 { get; set; }
        public string? EndpointId { get; set; }
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
}
