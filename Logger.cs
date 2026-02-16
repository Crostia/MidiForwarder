using System.Diagnostics;

namespace MidiForwarder
{
    public static class Logger
    {
        private static readonly string LogDirectory;
        private static readonly string LogFilePath;
        private static readonly int CurrentProcessId;
        private static readonly object LockObject = new();
        private const int MaxLogDays = 7; // 保留最近7天的日志
        private const int MaxLogFilesPerDay = 10; // 每天最多保留10个日志文件（不含正在使用的）

        static Logger()
    {
        CurrentProcessId = Environment.ProcessId;
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MidiForwarder",
            "log"
        );
        
        EnsureLogDirectoryExists();
        LogFilePath = GenerateLogFilePath();
        // 异步清理旧日志，避免阻塞程序启动
        _ = Task.Run(CleanupOldLogs);
    }

        private static void EnsureLogDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch { }
        }

        /// <summary>
        /// 生成包含PID的日志文件路径，格式：log_YYYYMMDD_PID_XXXXXXXX.txt
        /// </summary>
        private static string GenerateLogFilePath()
        {
            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var randomSuffix = Guid.NewGuid().ToString("N")[..8];
            var fileName = $"log_{dateStr}_{CurrentProcessId}_{randomSuffix}.txt";
            return Path.Combine(LogDirectory, fileName);
        }

        /// <summary>
        /// 从文件名解析PID
        /// 文件名格式：log_YYYYMMDD_PID_XXXXXXXX.txt
        /// </summary>
        private static int? ParseProcessIdFromFileName(string fileName)
        {
            try
            {
                // 去掉扩展名
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                // 分割：log_20260215_12345_a3f7b2c1
                var parts = nameWithoutExt.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[2], out int pid))
                {
                    return pid;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 检查进程是否仍在运行
        /// </summary>
        private static bool IsProcessRunning(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return process != null && !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清理旧日志文件
        /// 策略：
        /// 1. 删除超过 MaxLogDays 天的日志
        /// 2. 跳过正在使用的日志（进程仍在运行）
        /// 3. 对于当天的日志，如果超过 MaxLogFilesPerDay 个（不含正在使用的），只保留最新的
        /// 4. 对于其他日期的日志，每天只保留最新的一个（不含正在使用的）
        /// </summary>
        private static void CleanupOldLogs()
        {
            try
            {
                var logFiles = Directory.GetFiles(LogDirectory, "log_*.txt")
                    .Select(f => new FileInfo(f))
                    .ToList();

                var cutoffDate = DateTime.Now.AddDays(-MaxLogDays);
                var today = DateTime.Now.Date;

                // 1. 删除超过 MaxLogDays 天的日志（无论是否在使用）
                foreach (var file in logFiles.Where(f => f.CreationTime < cutoffDate))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch { }
                }

                // 重新获取文件列表（可能已删除一些）
                logFiles = [.. Directory.GetFiles(LogDirectory, "log_*.txt")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.CreationTime >= cutoffDate)];

                // 2. 筛选出可以删除的日志（进程已退出的）
                var deletableFiles = logFiles.Where(f =>
                {
                    var pid = ParseProcessIdFromFileName(f.Name);
                    // 如果解析不出PID，或者进程已退出，则可以删除
                    return pid == null || !IsProcessRunning(pid.Value);
                }).ToList();

                // 3. 筛选出需要保护的日志（进程仍在运行的）
                var protectedFiles = logFiles.Where(f =>
                {
                    var pid = ParseProcessIdFromFileName(f.Name);
                    return pid != null && IsProcessRunning(pid.Value);
                }).ToList();

                // 4. 对于当天的可删除日志，如果过多则只保留最新的
                var todayDeletableFiles = deletableFiles
                    .Where(f => f.CreationTime.Date == today)
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (todayDeletableFiles.Count > MaxLogFilesPerDay)
                {
                    foreach (var file in todayDeletableFiles.Skip(MaxLogFilesPerDay))
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch { }
                    }
                }

                // 5. 对于其他日期的可删除日志，每天只保留最新的一个
                var otherDaysDeletableFiles = deletableFiles
                    .Where(f => f.CreationTime.Date != today)
                    .GroupBy(f => f.CreationTime.Date)
                    .ToList();

                foreach (var dayGroup in otherDaysDeletableFiles)
                {
                    var filesToDelete = dayGroup
                        .OrderByDescending(f => f.CreationTime)
                        .Skip(1)
                        .ToList();

                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 写入文件日志（仅本地文件，不显示在界面）
        /// </summary>
        public static void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }

        /// <summary>
        /// 写入文件日志（仅本地文件，不显示在界面）
        /// </summary>
        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// 写入文件日志（仅本地文件，不显示在界面）
        /// </summary>
        public static void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        /// <summary>
        /// 写入文件日志（仅本地文件，不显示在界面）
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            WriteLog("ERROR", $"{message} - Exception: {ex.Message}");
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                
                lock (LockObject)
                {
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// 应用日志 - 同时显示在界面和写入文件
    /// </summary>
    public static class AppLog
    {
        private static MainFormLayout? layout;

        public static void SetLayout(MainFormLayout? formLayout)
        {
            layout = formLayout;
        }

        /// <summary>
        /// 信息日志 - 显示在界面并写入文件
        /// </summary>
        public static void Info(string message)
        {
            Logger.Info(message);
            layout?.LogMessage(message);
        }

        /// <summary>
        /// 错误日志 - 显示在界面并写入文件
        /// </summary>
        public static void Error(string message)
        {
            Logger.Error(message);
            layout?.LogMessage($"[错误] {message}");
        }

        /// <summary>
        /// 设备消息日志 - 高频消息，仅显示在界面，不写入文件
        /// </summary>
        public static void DeviceMessage(string message)
        {
            // 不写入文件，仅显示在界面
            layout?.LogMessage(message);
        }

        /// <summary>
        /// MIDI消息日志 - 高频消息，仅显示在界面，不写入文件
        /// </summary>
        public static void MidiMessage(string message)
        {
            // 不写入文件，仅显示在界面
            layout?.LogMessage(message);
        }
    }
}