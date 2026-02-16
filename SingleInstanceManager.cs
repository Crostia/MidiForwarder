using System.Runtime.InteropServices;

namespace MidiForwarder
{
    /// <summary>
    /// 单实例管理器 - 确保应用程序只有一个实例运行
    /// </summary>
    public partial class SingleInstanceManager : IDisposable
    {
        private const string MutexName = "MidiForwarder_SingleInstance_Mutex";
        private Mutex? mutex;
        private bool isDisposed = false;
        private bool isFirstInstance = false;

        /// <summary>
        /// 是否是第一个实例
        /// </summary>
        public bool IsFirstInstance => isFirstInstance;

        /// <summary>
        /// 初始化单实例管理器
        /// </summary>
        /// <returns>如果是第一个实例返回true，否则返回false</returns>
        public bool Initialize()
        {
            // 尝试创建互斥锁以实现单实例控制
            bool createdNew;
            try
            {
                mutex = new Mutex(true, MutexName, out createdNew);
                Logger.Info($"SingleInstanceManager: 互斥锁创建结果: createdNew = {createdNew}");
            }
            catch (AbandonedMutexException)
            {
                // 互斥锁被遗弃（之前的实例崩溃未释放），获取所有权
                Logger.Info("SingleInstanceManager: 互斥锁被遗弃，重新获取所有权");
                mutex = new Mutex(true, MutexName, out _);
                createdNew = true;
            }

            isFirstInstance = createdNew;
            return createdNew;
        }

        /// <summary>
        /// 激活已存在的实例窗口
        /// </summary>
        public static void ActivateExistingInstance()
        {
            Logger.Info("SingleInstanceManager: 开始激活已有实例");
            try
            {
                var windowHandle = FindWindowByProcess();
                Logger.Info($"SingleInstanceManager: 查找到的窗口句柄 = {windowHandle}");

                if (windowHandle != IntPtr.Zero)
                {
                    // 恢复并显示窗口（如果隐藏或最小化）
                    var showResult = ShowWindow(windowHandle, SW_RESTORE);
                    Logger.Info($"SingleInstanceManager: ShowWindow 结果 = {showResult}");

                    // 激活窗口
                    var fgResult = SetForegroundWindow(windowHandle);
                    Logger.Info($"SingleInstanceManager: SetForegroundWindow 结果 = {fgResult}");
                }
                else
                {
                    Logger.Error("SingleInstanceManager: 未找到窗口句柄");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("SingleInstanceManager: 激活已有实例时发生异常", ex);
            }
        }

        /// <summary>
        /// 通过进程名查找窗口句柄
        /// </summary>
        private static IntPtr FindWindowByProcess()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processName = Path.GetFileNameWithoutExtension(currentProcess.ProcessName);
            Logger.Info($"SingleInstanceManager: 当前进程ID = {currentProcess.Id}, 进程名 = {processName}");

            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            Logger.Info($"SingleInstanceManager: 找到 {processes.Length} 个同名进程");

            foreach (var process in processes)
            {
                Logger.Info($"SingleInstanceManager: 检查进程 ID = {process.Id}");
                if (process.Id != currentProcess.Id)
                {
                    Logger.Info($"SingleInstanceManager: 发现其他实例，进程ID = {process.Id}, MainWindowHandle = {process.MainWindowHandle}");

                    // 尝试获取主窗口句柄（即使窗口被隐藏，主窗口句柄通常也存在）
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        Logger.Info($"SingleInstanceManager: 使用 MainWindowHandle = {process.MainWindowHandle}");
                        return process.MainWindowHandle;
                    }

                    // 如果 MainWindowHandle 为 0，尝试枚举进程的所有窗口
                    Logger.Info("SingleInstanceManager: MainWindowHandle 为 0，开始枚举窗口");
                    var handle = IntPtr.Zero;
                    EnumWindows((hWnd, lParam) =>
                    {
                        uint threadId = GetWindowThreadProcessId(hWnd, out uint processId);
                        if (threadId != 0 && processId == process.Id)
                        {
                            // 不检查窗口是否可见，因为窗口可能被隐藏
                            // 只检查是否是顶层窗口
                            if (GetParent(hWnd) == IntPtr.Zero)
                            {
                                handle = hWnd;
                                Logger.Info($"SingleInstanceManager: 枚举找到窗口句柄 = {hWnd}");
                                return false; // 停止枚举
                            }
                        }
                        return true; // 继续枚举
                    }, IntPtr.Zero);

                    if (handle != IntPtr.Zero)
                    {
                        return handle;
                    }
                    Logger.Info("SingleInstanceManager: 枚举未找到窗口");
                }
                else
                {
                    Logger.Info($"SingleInstanceManager: 跳过当前进程 ID = {process.Id}");
                }
            }

            Logger.Info("SingleInstanceManager: 未找到其他实例的窗口");
            return IntPtr.Zero;
        }

        private const int SW_RESTORE = 9;

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [LibraryImport("user32.dll")]
        private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [LibraryImport("user32.dll")]
        private static partial IntPtr GetParent(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public void Dispose()
        {
            if (isDisposed) return;

            mutex?.ReleaseMutex();
            mutex?.Dispose();
            mutex = null;

            isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
