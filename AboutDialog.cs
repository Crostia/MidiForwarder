using System.Reflection;

namespace MidiForwarder
{
    public class AboutDialog : IDisposable
    {
        private static AboutDialog? _instance;
        private static readonly object _lock = new();

        private Form? aboutForm;
        private Label? titleLabel;
        private Label? versionPrefixLabel;
        private Label? versionNumberLabel;
        private Label? authorPrefixLabel;
        private Label? authorNameLabel;
        private LinkLabel? authorLinkLabel;
        private Label? copyrightLabel;
        private LinkLabel? websiteLinkLabel;
        private Label? descriptionLabel;
        private Button? okButton;
        private PictureBox? iconPictureBox;

        // 软件信息配置 - 可以在这里修改
        public string SoftwareName { get; set; } = LocalizationManager.GetString("AboutSoftwareName");
        public string Version { get; set; } = GetAssemblyVersion();
        public string Author { get; set; } = "Crostia";

        private static string GetAssemblyVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString(3) ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }
        public string? AuthorUrl { get; set; } = "https://github.com/Crostia";
        public string Description { get; set; } = LocalizationManager.GetString("AboutDescription");
        public string? Copyright { get; set; } = null;
        public string? Website { get; set; } = null;
        public Image? CustomIcon { get; set; } = null;

        private AboutDialog()
        {
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        public static AboutDialog Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new AboutDialog();
                    return _instance;
                }
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (aboutForm != null && !aboutForm.IsDisposed)
            {
                aboutForm.Invoke(new Action(UpdateLocalizedText));
            }
        }

        public void Show()
        {
            if (aboutForm != null && !aboutForm.IsDisposed)
            {
                aboutForm.BringToFront();
                aboutForm.Focus();
                return;
            }

            CreateForm();
            UpdateLocalizedText();
            aboutForm?.ShowDialog();
        }

        private void CreateForm()
        {
            aboutForm = new Form
            {
                Size = new Size(400, 280),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = true
            };

            aboutForm.FormClosed += (s, e) =>
            {
                // 只清理引用，不调用 Dispose，因为窗体已经关闭
                aboutForm = null;
                titleLabel = null;
                versionPrefixLabel = null;
                versionNumberLabel = null;
                authorPrefixLabel = null;
                authorNameLabel = null;
                authorLinkLabel = null;
                copyrightLabel = null;
                websiteLinkLabel = null;
                descriptionLabel = null;
                okButton = null;
                iconPictureBox = null;
            };

            iconPictureBox = new PictureBox
            {
                Image = CustomIcon ?? LoadEmbeddedIcon() ?? SystemIcons.Information.ToBitmap(),
                Size = new Size(48, 48),
                Location = new Point(30, 30),
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            titleLabel = new Label
            {
                Font = new Font("Microsoft YaHei", 14, FontStyle.Bold),
                Location = new Point(100, 30),
                AutoSize = true
            };

            versionPrefixLabel = new Label
            {
                Location = new Point(100, 60),
                AutoSize = true,
                Font = new Font("Microsoft YaHei", 9F),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            versionNumberLabel = new Label
            {
                Text = Version,
                Location = new Point(0, 60),
                AutoSize = true,
                Font = new Font("Microsoft YaHei", 9F),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            authorPrefixLabel = new Label
            {
                Location = new Point(100, 85),
                AutoSize = true,
                Font = new Font("Microsoft YaHei", 9F),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            authorNameLabel = new Label
            {
                Text = Author,
                Location = new Point(0, 85),
                AutoSize = true,
                Font = new Font("Microsoft YaHei", 9F),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            if (!string.IsNullOrEmpty(AuthorUrl))
            {
                authorLinkLabel = new LinkLabel
                {
                    Text = $"({AuthorUrl})",
                    Location = new Point(0, 85),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei", 9F)
                };
                authorLinkLabel.LinkClicked += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AuthorUrl!) { UseShellExecute = true });
                    }
                    catch { }
                };
            }

            if (!string.IsNullOrEmpty(Copyright))
            {
                copyrightLabel = new Label
                {
                    Text = Copyright,
                    Location = new Point(100, 115),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei", 9F)
                };
            }

            if (!string.IsNullOrEmpty(Website))
            {
                int websiteY = string.IsNullOrEmpty(Copyright) ? 115 : 140;
                websiteLinkLabel = new LinkLabel
                {
                    Text = Website,
                    Location = new Point(100, websiteY),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei", 9F)
                };
                websiteLinkLabel.LinkClicked += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Website!) { UseShellExecute = true });
                    }
                    catch { }
                };
            }

            int descriptionY = 115;
            if (!string.IsNullOrEmpty(Copyright)) descriptionY += 25;
            if (!string.IsNullOrEmpty(Website)) descriptionY += 25;

            descriptionLabel = new Label
            {
                Location = new Point(100, descriptionY),
                Size = new Size(270, 50),
                Font = new Font("Microsoft YaHei", 9F)
            };

            okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(150, 200),
                Size = new Size(100, 30),
                Font = new Font("Microsoft YaHei", 9F)
            };
            okButton.Click += (s, e) => aboutForm?.Close();

            aboutForm.Controls.Add(iconPictureBox);
            aboutForm.Controls.Add(titleLabel);
            aboutForm.Controls.Add(versionPrefixLabel);
            aboutForm.Controls.Add(versionNumberLabel);
            aboutForm.Controls.Add(authorPrefixLabel);
            aboutForm.Controls.Add(authorNameLabel);

            if (authorLinkLabel != null)
                aboutForm.Controls.Add(authorLinkLabel);

            if (copyrightLabel != null)
                aboutForm.Controls.Add(copyrightLabel);

            if (websiteLinkLabel != null)
                aboutForm.Controls.Add(websiteLinkLabel);

            aboutForm.Controls.Add(descriptionLabel);
            aboutForm.Controls.Add(okButton);

            aboutForm.AcceptButton = okButton;
        }

        private void UpdateLocalizedText()
        {
            if (aboutForm == null || aboutForm.IsDisposed) return;

            aboutForm.Text = LocalizationManager.GetString("AboutTitle");

            if (titleLabel != null)
                titleLabel.Text = LocalizationManager.GetString("AboutSoftwareName");

            // 重新计算版本相关控件的位置
            if (versionPrefixLabel != null && versionNumberLabel != null)
            {
                versionPrefixLabel.Text = LocalizationManager.GetString("AboutVersionPrefix");
                using var g = aboutForm.CreateGraphics();
                int versionX = 100 + (int)g.MeasureString(versionPrefixLabel.Text, versionPrefixLabel.Font).Width;
                versionNumberLabel.Location = new Point(versionX, 60);
            }

            if (authorPrefixLabel != null)
                authorPrefixLabel.Text = LocalizationManager.GetString("AboutAuthor");

            // 重新计算作者相关控件的位置
            if (authorPrefixLabel != null && authorNameLabel != null)
            {
                using var g = aboutForm.CreateGraphics();
                int currentX = 100 + (int)g.MeasureString(authorPrefixLabel.Text, authorPrefixLabel.Font).Width;
                authorNameLabel.Location = new Point(currentX, 85);
                currentX += (int)g.MeasureString(authorNameLabel.Text, authorNameLabel.Font).Width;

                if (authorLinkLabel != null)
                {
                    authorLinkLabel.Location = new Point(currentX, 85);
                }
            }

            if (descriptionLabel != null)
                descriptionLabel.Text = LocalizationManager.GetString("AboutDescription");

            if (okButton != null)
                okButton.Text = LocalizationManager.GetString("AboutOkButton");
        }

        private static Image? LoadEmbeddedIcon()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "MidiForwarder.default_form_icon.png";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    return Image.FromStream(stream);
                }
            }
            catch { }

            return null;
        }

        public void Dispose()
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            if (aboutForm != null && !aboutForm.IsDisposed)
            {
                aboutForm.Dispose();
            }
            aboutForm = null;
            lock (_lock)
            {
                _instance = null;
            }
            GC.SuppressFinalize(this);
        }
    }
}
