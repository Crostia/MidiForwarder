namespace MidiForwarder
{
    /// <summary>
    /// 主窗体类 - 负责UI展示和事件转发
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly MainFormController controller;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MainForm(string[] args)
        {
            // 创建控制器
            controller = new MainFormController(args);

            // 应用启动时的窗口状态设置
            controller.ApplyStartupWindowState(this);

            // 如果需要初始化托盘图标
            if (controller.ShouldInitializeTray)
            {
                controller.InitializeTrayIcon();
            }

            // 初始化UI
            InitializeOnUIThread();
        }

        /// <summary>
        /// 在UI线程上初始化
        /// </summary>
        private void InitializeOnUIThread()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(InitializeOnUIThread));
                return;
            }

            controller.Initialize(this);

            // 订阅控制器事件
            controller.RequestShowWindow += (s, e) => ShowFromTray();
            controller.RequestExit += (s, e) => Close();

            // 订阅窗体关闭事件
            FormClosing += MainForm_FormClosing;
        }

        /// <summary>
        /// 从托盘显示窗口
        /// </summary>
        private void ShowFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        /// <summary>
        /// 窗体关闭事件处理
        /// </summary>
        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // 让控制器处理关闭逻辑
            bool shouldContinueClosing = controller.HandleFormClosing(e);

            if (!shouldContinueClosing)
            {
                // 只是隐藏到托盘
                Hide();
            }
            else
            {
                // 真正关闭，释放资源
                controller.Dispose();
            }
        }
    }
}
