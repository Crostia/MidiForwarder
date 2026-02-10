namespace MidiForwarder
{
    public class MainFormLayout
    {
        private readonly Form form;

        public ComboBox InputComboBox { get; private set; } = null!;
        public ComboBox OutputComboBox { get; private set; } = null!;
        public Button ConnectButton { get; private set; } = null!;
        public Button RefreshButton { get; private set; } = null!;
        public CheckBox AutoConnectCheckBox { get; private set; } = null!;
        public Label StatusLabel { get; private set; } = null!;
        public TextBox LogTextBox { get; private set; } = null!;

        // GroupBoxes for localization updates
        private GroupBox inputGroupBox = null!;
        private GroupBox outputGroupBox = null!;
        private GroupBox logGroupBox = null!;
        private Label inputLabel = null!;
        private Label outputLabel = null!;

        public event EventHandler? ConnectButtonClicked;
        public event EventHandler? RefreshButtonClicked;
        public event EventHandler? AutoConnectChanged;
        public event EventHandler? InputSelectionChanged;
        public event EventHandler? OutputSelectionChanged;

        public MainFormLayout(Form form)
        {
            this.form = form;
            InitializeLayout();
            LocalizationManager.LanguageChanged += (s, e) => UpdateLocalizedText();
        }

        private void InitializeLayout()
        {
            form.Text = LocalizationManager.GetString("AppTitle");
            form.Size = new Size(600, 400);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MaximizeBox = false;
            form.MinimizeBox = false;

            // 输入设备区域
            inputGroupBox = new GroupBox
            {
                Text = LocalizationManager.GetString("InputDeviceGroup"),
                Location = new Point(10, 10),
                Size = new Size(270, 120)
            };

            inputLabel = new Label
            {
                Text = LocalizationManager.GetString("SelectInputDevice"),
                Location = new Point(10, 25),
                AutoSize = true
            };

            InputComboBox = new ComboBox
            {
                Name = "inputComboBox",
                Location = new Point(10, 50),
                Size = new Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // 输出设备区域
            outputGroupBox = new GroupBox
            {
                Text = LocalizationManager.GetString("OutputDeviceGroup"),
                Location = new Point(300, 10),
                Size = new Size(270, 120)
            };

            outputLabel = new Label
            {
                Text = LocalizationManager.GetString("SelectOutputDevice"),
                Location = new Point(10, 25),
                AutoSize = true
            };

            OutputComboBox = new ComboBox
            {
                Name = "outputComboBox",
                Location = new Point(10, 50),
                Size = new Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // 日志区域
            logGroupBox = new GroupBox
            {
                Text = LocalizationManager.GetString("LogGroup"),
                Location = new Point(10, 140),
                Size = new Size(560, 170)
            };

            LogTextBox = new TextBox
            {
                Name = "logTextBox",
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Location = new Point(10, 25),
                Size = new Size(540, 130),
                Font = new Font("Consolas", 9)
            };

            // 自动连接复选框
            AutoConnectCheckBox = new CheckBox
            {
                Name = "autoConnectCheckBox",
                Text = LocalizationManager.GetString("AutoConnectOnStartup"),
                Location = new Point(15, 323),
                AutoSize = true
            };

            // 状态标签
            StatusLabel = new Label
            {
                Name = "statusLabel",
                Text = LocalizationManager.GetString("StatusDisconnected"),
                Location = new Point(150, 324),
                AutoSize = true,
                ForeColor = Color.Red
            };

            // 控制按钮
            ConnectButton = new Button
            {
                Name = "connectButton",
                Text = LocalizationManager.GetString("ConnectButton"),
                Location = new Point(320, 317),
                Size = new Size(120, 30),
                Enabled = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0, 1, 0, 0)
            };

            RefreshButton = new Button
            {
                Name = "refreshButton",
                Text = LocalizationManager.GetString("RefreshDevicesButton"),
                Location = new Point(450, 317),
                Size = new Size(120, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0, 2, 0, 0)
            };

            // 组装控件
            inputGroupBox.Controls.Add(inputLabel);
            inputGroupBox.Controls.Add(InputComboBox);

            outputGroupBox.Controls.Add(outputLabel);
            outputGroupBox.Controls.Add(OutputComboBox);

            logGroupBox.Controls.Add(LogTextBox);

            form.Controls.Add(inputGroupBox);
            form.Controls.Add(outputGroupBox);
            form.Controls.Add(ConnectButton);
            form.Controls.Add(RefreshButton);
            form.Controls.Add(AutoConnectCheckBox);
            form.Controls.Add(StatusLabel);
            form.Controls.Add(logGroupBox);

            // 绑定事件
            ConnectButton.Click += (s, e) => ConnectButtonClicked?.Invoke(this, e);
            RefreshButton.Click += (s, e) => RefreshButtonClicked?.Invoke(this, e);
            AutoConnectCheckBox.CheckedChanged += (s, e) => AutoConnectChanged?.Invoke(this, e);
            InputComboBox.SelectedIndexChanged += (s, e) => InputSelectionChanged?.Invoke(this, e);
            OutputComboBox.SelectedIndexChanged += (s, e) => OutputSelectionChanged?.Invoke(this, e);
        }

        private void UpdateLocalizedText()
        {
            form.Text = LocalizationManager.GetString("AppTitle");
            inputGroupBox.Text = LocalizationManager.GetString("InputDeviceGroup");
            inputLabel.Text = LocalizationManager.GetString("SelectInputDevice");
            outputGroupBox.Text = LocalizationManager.GetString("OutputDeviceGroup");
            outputLabel.Text = LocalizationManager.GetString("SelectOutputDevice");
            logGroupBox.Text = LocalizationManager.GetString("LogGroup");
            AutoConnectCheckBox.Text = LocalizationManager.GetString("AutoConnectOnStartup");
            RefreshButton.Text = LocalizationManager.GetString("RefreshDevicesButton");

            // 根据当前状态更新状态标签和连接按钮
            bool isConnected = ConnectButton.Text == LocalizationManager.GetString("DisconnectButton") ||
                              (ConnectButton.Text != LocalizationManager.GetString("ConnectButton") && StatusLabel.ForeColor == Color.Green);
            SetConnectedState(isConnected);
        }

        public void UpdateConnectButtonState(bool enabled)
        {
            ConnectButton.Enabled = enabled;
        }

        public void SetConnectedState(bool connected)
        {
            ConnectButton.Text = connected ? LocalizationManager.GetString("DisconnectButton") : LocalizationManager.GetString("ConnectButton");
            StatusLabel.Text = connected ? LocalizationManager.GetString("StatusConnected") : LocalizationManager.GetString("StatusDisconnected");
            StatusLabel.ForeColor = connected ? Color.Green : Color.Red;
        }

        public void LogMessage(string message)
        {
            if (LogTextBox.InvokeRequired)
            {
                LogTextBox.Invoke(new Action<string>(LogMessage), message);
            }
            else
            {
                LogTextBox.AppendText($"{message}\r\n");
                LogTextBox.ScrollToCaret();
            }
        }

        public void ClearLog()
        {
            if (LogTextBox.InvokeRequired)
            {
                LogTextBox.Invoke(new Action(ClearLog));
            }
            else
            {
                LogTextBox.Clear();
            }
        }
    }
}
