namespace MidiForwarder
{
    public class UpdateDialog : Form
    {
        private readonly string currentVersion;
        private readonly string latestVersion;
        private readonly string releaseNotes;
        private readonly string downloadUrl;

        // 控件引用，用于语言切换时更新文本
        private Label? versionLabel;
        private Label? releaseNotesTitleLabel;
        private CheckBox? ignoreVersionCheckBox;
        private Button? yesButton;
        private Button? noButton;

        public bool ShouldOpenDownloadPage { get; private set; } = false;
        public bool ShouldIgnoreThisVersion { get; private set; } = false;

        public UpdateDialog(string currentVersion, string latestVersion, string releaseNotes, string downloadUrl)
        {
            this.currentVersion = currentVersion;
            this.latestVersion = latestVersion;
            this.releaseNotes = releaseNotes;
            this.downloadUrl = downloadUrl;

            // 订阅语言切换事件
            LocalizationManager.LanguageChanged += OnLanguageChanged;

            InitializeComponents();
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (!IsDisposed && InvokeRequired)
            {
                Invoke(new Action(UpdateLocalizedText));
            }
            else if (!IsDisposed)
            {
                UpdateLocalizedText();
            }
        }

        private void UpdateLocalizedText()
        {
            if (IsDisposed) return;

            Text = LocalizationManager.GetString("UpdateAvailableTitle");

            if (versionLabel != null)
            {
                versionLabel.Text = string.Format(LocalizationManager.GetString("UpdateAvailableMessageCurrentVersion"), currentVersion) + "\n" +
                                   string.Format(LocalizationManager.GetString("UpdateAvailableMessageLatestVersion"), latestVersion);
            }

            if (releaseNotesTitleLabel != null)
                releaseNotesTitleLabel.Text = LocalizationManager.GetString("UpdateReleaseNotes");

            if (ignoreVersionCheckBox != null)
                ignoreVersionCheckBox.Text = LocalizationManager.GetString("UpdateIgnoreThisVersion");

            if (yesButton != null)
                yesButton.Text = LocalizationManager.GetString("UpdateDialogYesButton");

            if (noButton != null)
                noButton.Text = LocalizationManager.GetString("UpdateDialogNoButton");
        }

        private void InitializeComponents()
        {
            // 设置对话框属性
            Text = LocalizationManager.GetString("UpdateAvailableTitle");
            Size = new Size(520, 480);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            Padding = new Padding(20);

            // 版本信息标签（小字号）
            versionLabel = new Label
            {
                Text = string.Format(LocalizationManager.GetString("UpdateAvailableMessageCurrentVersion"), currentVersion) + "\n" +
                       string.Format(LocalizationManager.GetString("UpdateAvailableMessageLatestVersion"), latestVersion),
                AutoSize = false,
                Size = new Size(460, 35),
                Location = new Point(20, 15),
                Font = new Font("Microsoft YaHei", 9),
                TextAlign = ContentAlignment.TopLeft
            };

            // 更新内容标题
            releaseNotesTitleLabel = new Label
            {
                Text = LocalizationManager.GetString("UpdateReleaseNotes"),
                AutoSize = true,
                Location = new Point(20, 55),
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold)
            };

            // 更新内容文本框（支持滚动，不可选中）
            var releaseNotesTextBox = new TextBox
            {
                Text = releaseNotes,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(467, 315),
                Location = new Point(20, 77),
                Font = new Font("Microsoft YaHei", 9),
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                TabStop = false
            };
            // 禁止文本选中
            releaseNotesTextBox.MouseDown += (s, e) => { };
            releaseNotesTextBox.MouseUp += (s, e) => { };
            releaseNotesTextBox.KeyDown += (s, e) => e.Handled = true;
            releaseNotesTextBox.GotFocus += (s, e) => ActiveControl = null;

            // 忽略此版本复选框
            ignoreVersionCheckBox = new CheckBox
            {
                Text = LocalizationManager.GetString("UpdateIgnoreThisVersion"),
                AutoSize = true,
                Location = new Point(20, 400),
                Font = new Font("Microsoft YaHei", 9)
            };

            // 是按钮
            yesButton = new Button
            {
                Text = LocalizationManager.GetString("UpdateDialogYesButton"),
                Size = new Size(100, 32),
                Location = new Point(280, 400),
                DialogResult = DialogResult.Yes,
                Font = new Font("Microsoft YaHei", 9)
            };
            yesButton.Click += (s, e) =>
            {
                ShouldOpenDownloadPage = true;
                ShouldIgnoreThisVersion = ignoreVersionCheckBox?.Checked ?? false;
                DialogResult = DialogResult.Yes;
                Close();
            };

            // 否按钮
            noButton = new Button
            {
                Text = LocalizationManager.GetString("UpdateDialogNoButton"),
                Size = new Size(100, 32),
                Location = new Point(390, 400),
                DialogResult = DialogResult.No,
                Font = new Font("Microsoft YaHei", 9)
            };
            noButton.Click += (s, e) =>
            {
                ShouldOpenDownloadPage = false;
                ShouldIgnoreThisVersion = ignoreVersionCheckBox?.Checked ?? false;
                DialogResult = DialogResult.No;
                Close();
            };

            // 添加控件
            if (versionLabel != null) Controls.Add(versionLabel);
            if (releaseNotesTitleLabel != null) Controls.Add(releaseNotesTitleLabel);
            Controls.Add(releaseNotesTextBox);
            if (ignoreVersionCheckBox != null) Controls.Add(ignoreVersionCheckBox);
            if (yesButton != null) Controls.Add(yesButton);
            if (noButton != null) Controls.Add(noButton);

            // 设置默认按钮
            AcceptButton = yesButton;
            CancelButton = noButton;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 取消订阅语言切换事件，防止内存泄漏
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            base.OnFormClosed(e);
        }

        public static (bool shouldDownload, bool shouldIgnore) ShowUpdateDialog(string currentVersion, string latestVersion, string releaseNotes, string downloadUrl)
        {
            using var dialog = new UpdateDialog(currentVersion, latestVersion, releaseNotes, downloadUrl);
            dialog.ShowDialog();
            return (dialog.ShouldOpenDownloadPage, dialog.ShouldIgnoreThisVersion);
        }
    }
}
