namespace MidiForwarder
{
    /// <summary>
    /// 程序入口类
    /// </summary>
    internal static class ProgramEntry
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            // 使用单实例管理器确保只有一个实例运行
            using var singleInstanceManager = new SingleInstanceManager();

            if (!singleInstanceManager.Initialize())
            {
                // 不是第一个实例，激活已有实例并退出
                SingleInstanceManager.ActivateExistingInstance();
                Logger.Info("已有实例激活完成，当前实例退出");
                return;
            }

            // 是第一个实例，记录日志并启动应用程序
            Logger.Info($"========================================");
            Logger.Info($"程序启动 - 参数: {string.Join(" ", args)}");

            try
            {
                Application.Run(new MainForm(args));
            }
            finally
            {
                Logger.Info("程序退出");
            }
        }
    }
}
