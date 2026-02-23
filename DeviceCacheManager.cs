namespace MidiForwarder
{
    /// <summary>
    /// 设备缓存管理类 - 统一管理MIDI设备列表缓存
    /// 支持 MIDI 1.0 和 MIDI 2.0 API，通过事件通知其他组件
    /// </summary>
    public class DeviceCacheManager : IDisposable
    {
        // 缓存数据
        private List<MidiDeviceInfo> cachedInputDevices = [];
        private List<MidiDeviceInfo> cachedOutputDevices = [];
        private readonly object cacheLock = new();

        // 定时器
        private System.Windows.Forms.Timer? autoRefreshTimer;
        private const int AutoRefreshInterval = 1000; // 每秒检测一次

        // 状态标记
        private bool isDisposed = false;
        private MidiApiType currentApiType;

        /// <summary>
        /// 设备列表变更事件（输入或输出设备有变更时触发）
        /// </summary>
        public event EventHandler? DevicesChanged;

        /// <summary>
        /// 当前使用的 MIDI API 类型
        /// </summary>
        public MidiApiType CurrentApiType => currentApiType;

        /// <summary>
        /// 当前缓存的输入设备列表（只读副本）
        /// </summary>
        public IReadOnlyList<MidiDeviceInfo> CachedInputDevices
        {
            get
            {
                lock (cacheLock)
                {
                    return cachedInputDevices.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// 当前缓存的输出设备列表（只读副本）
        /// </summary>
        public IReadOnlyList<MidiDeviceInfo> CachedOutputDevices
        {
            get
            {
                lock (cacheLock)
                {
                    return cachedOutputDevices.AsReadOnly();
                }
            }
        }

        public DeviceCacheManager()
        {
            // 检测当前可用的 MIDI API
            currentApiType = MidiApiFactory.IsMidi20Available ? MidiApiType.Midi20 : MidiApiType.Midi10;
            Logger.Info($"DeviceCacheManager: 使用 {(currentApiType == MidiApiType.Midi20 ? "MIDI 2.0" : "MIDI 1.0")} API 获取设备列表");
        }

        /// <summary>
        /// 获取当前缓存的设备列表
        /// </summary>
        public (List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices) GetCachedDevices()
        {
            lock (cacheLock)
            {
                return (new List<MidiDeviceInfo>(cachedInputDevices),
                        new List<MidiDeviceInfo>(cachedOutputDevices));
            }
        }

        /// <summary>
        /// 启动自动刷新定时器
        /// </summary>
        public void StartAutoRefresh()
        {
            if (autoRefreshTimer?.Enabled == true) return;

            autoRefreshTimer?.Dispose();
            autoRefreshTimer = new System.Windows.Forms.Timer
            {
                Interval = AutoRefreshInterval
            };

            autoRefreshTimer.Tick += (s, e) => PerformAutoRefresh();
            autoRefreshTimer.Start();

            Logger.Info("DeviceCacheManager: 自动刷新已启动");
        }

        /// <summary>
        /// 停止自动刷新定时器
        /// </summary>
        public void StopAutoRefresh()
        {
            autoRefreshTimer?.Stop();
            autoRefreshTimer?.Dispose();
            autoRefreshTimer = null;

            Logger.Info("DeviceCacheManager: 自动刷新已停止");
        }

        /// <summary>
        /// 手动刷新 - 立即刷新缓存并返回最新设备列表
        /// </summary>
        public (List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices) ManualRefresh()
        {
            Logger.Info("DeviceCacheManager: 执行手动刷新");

            // 根据当前 API 类型获取设备列表
            var (newInputDevices, newOutputDevices) = GetDevicesFromCurrentApi();

            // 更新缓存
            lock (cacheLock)
            {
                cachedInputDevices = newInputDevices;
                cachedOutputDevices = newOutputDevices;
            }

            Logger.Info($"DeviceCacheManager: 手动刷新完成 - 输入设备: {newInputDevices.Count}, 输出设备: {newOutputDevices.Count}");

            return (newInputDevices, newOutputDevices);
        }

        /// <summary>
        /// 根据当前 API 类型获取设备列表
        /// </summary>
        private (List<MidiDeviceInfo> inputDevices, List<MidiDeviceInfo> outputDevices) GetDevicesFromCurrentApi()
        {
            try
            {
                if (currentApiType == MidiApiType.Midi20 && MidiApiFactory.IsMidi20Available)
                {
#if WINDOWS_MIDI_SERVICES
                    var inputDevices = Midi20Manager.GetInputDevices();
                    var outputDevices = Midi20Manager.GetOutputDevices();
                    return (inputDevices, outputDevices);
#endif
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"使用 MIDI 2.0 API 获取设备失败，回退到 MIDI 1.0: {ex.Message}");
                currentApiType = MidiApiType.Midi10;
            }

            // 默认使用 MIDI 1.0
            var midi10InputDevices = Midi10Manager.GetInputDevices();
            var midi10OutputDevices = Midi10Manager.GetOutputDevices();
            return (midi10InputDevices, midi10OutputDevices);
        }

        /// <summary>
        /// 自动刷新 - 静默检测变更，有变更时触发事件通知UI主动获取
        /// </summary>
        private void PerformAutoRefresh()
        {
            var previousInputDevices = GetCachedDevices().inputDevices;
            var previousOutputDevices = GetCachedDevices().outputDevices;

            // 获取最新设备列表
            var (newInputDevices, newOutputDevices) = GetDevicesFromCurrentApi();

            // 检测是否有变更
            bool inputChanged = !AreDeviceListsEqual(previousInputDevices, newInputDevices);
            bool outputChanged = !AreDeviceListsEqual(previousOutputDevices, newOutputDevices);

            // 如果没有变更，直接返回
            if (!inputChanged && !outputChanged)
            {
                return;
            }

            // 更新缓存
            lock (cacheLock)
            {
                cachedInputDevices = newInputDevices;
                cachedOutputDevices = newOutputDevices;
            }

            Logger.Info($"DeviceCacheManager: 自动刷新检测到变更 - 输入设备变更: {inputChanged}, 输出设备变更: {outputChanged}");

            // 触发设备变更事件，通知UI主动获取缓存
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 比较两个设备列表是否相等
        /// </summary>
        private static bool AreDeviceListsEqual(List<MidiDeviceInfo> list1, List<MidiDeviceInfo> list2)
        {
            if (list1.Count != list2.Count)
                return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i].Id != list2[i].Id || list1[i].Name != list2[i].Name)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 根据设备名称查找设备ID
        /// </summary>
        public string? FindInputDeviceIdByName(string deviceName)
        {
            lock (cacheLock)
            {
                var device = cachedInputDevices.FirstOrDefault(d => d.Name == deviceName);
                return device?.Id;
            }
        }

        /// <summary>
        /// 根据设备名称查找设备ID
        /// </summary>
        public string? FindOutputDeviceIdByName(string deviceName)
        {
            lock (cacheLock)
            {
                var device = cachedOutputDevices.FirstOrDefault(d => d.Name == deviceName);
                return device?.Id;
            }
        }

        /// <summary>
        /// 根据设备ID查找设备名称
        /// </summary>
        public string? FindInputDeviceNameById(string deviceId)
        {
            lock (cacheLock)
            {
                var device = cachedInputDevices.FirstOrDefault(d => d.Id == deviceId);
                return device?.Name;
            }
        }

        /// <summary>
        /// 根据设备ID查找设备名称
        /// </summary>
        public string? FindOutputDeviceNameById(string deviceId)
        {
            lock (cacheLock)
            {
                var device = cachedOutputDevices.FirstOrDefault(d => d.Id == deviceId);
                return device?.Name;
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;

            StopAutoRefresh();
            isDisposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
