namespace MidiForwarder
{
    /// <summary>
    /// 设备缓存管理类 - 统一管理MIDI设备列表缓存
    /// 提供手动刷新和自动刷新两种方案，通过事件通知其他组件
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

        /// <summary>
        /// 设备列表变更事件（输入或输出设备有变更时触发）
        /// </summary>
        public event EventHandler? DevicesChanged;

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

            // 获取最新设备列表
            var newInputDevices = MidiManager.GetInputDevices();
            var newOutputDevices = MidiManager.GetOutputDevices();

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
        /// 自动刷新 - 静默检测变更，有变更时触发事件通知UI主动获取
        /// </summary>
        private void PerformAutoRefresh()
        {
            var previousInputDevices = GetCachedDevices().inputDevices;
            var previousOutputDevices = GetCachedDevices().outputDevices;

            // 获取最新设备列表
            var newInputDevices = MidiManager.GetInputDevices();
            var newOutputDevices = MidiManager.GetOutputDevices();

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
        public int? FindInputDeviceIdByName(string deviceName)
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
        public int? FindOutputDeviceIdByName(string deviceName)
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
        public string? FindInputDeviceNameById(int deviceId)
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
        public string? FindOutputDeviceNameById(int deviceId)
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
