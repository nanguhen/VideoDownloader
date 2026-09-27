// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoDownloader
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (Array.IndexOf(args, "--check") >= 0)
            {
                var config = AppSettings.Load();
                Console.WriteLine("YtDlpPath=" + config.YtDlpPath);
                Console.WriteLine("FfmpegPath=" + config.FfmpegPath);
                Console.WriteLine("FfprobePath=" + config.FfprobePath);
                Console.WriteLine("OutputFolder=" + config.OutputFolder);
                Console.WriteLine("CookiesPath=" + config.CookiesPath);
                Console.WriteLine("Proxy=" + config.Proxy);
                Console.WriteLine("ThemeMode=" + config.ThemeMode);
                Console.WriteLine("YtDlpExists=" + File.Exists(config.YtDlpPath));
                Console.WriteLine("FfmpegExists=" + File.Exists(config.FfmpegPath));
                Console.WriteLine("FfprobeExists=" + File.Exists(config.FfprobePath));
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class AppSettings
    {
        private static readonly string DefaultYtDlp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
        private static readonly string DefaultFfmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        private static readonly string DefaultFfprobe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");

        public string YtDlpPath { get; set; }
        public string FfmpegPath { get; set; }
        public string FfprobePath { get; set; }
        public string OutputFolder { get; set; }
        public string CookiesPath { get; set; }
        public string Proxy { get; set; }
        public int FormatMode { get; set; }
        public int ThemeMode { get; set; }

        public static string ConfigFile
        {
            get
            {
                var dir = Path.GetDirectoryName(typeof(AppSettings).Assembly.Location);
                return Path.Combine(dir, "VideoDownloader.config");
            }
        }

        public static AppSettings Load()
        {
            var settings = new AppSettings
            {
                YtDlpPath = DefaultYtDlp,
                FfmpegPath = DefaultFfmpeg,
                FfprobePath = DefaultFfprobe,
                OutputFolder = GetDefaultOutputFolder(),
                CookiesPath = string.Empty,
                Proxy = string.Empty,
                FormatMode = 0,
                ThemeMode = 0
            };

            try
            {
                if (File.Exists(ConfigFile))
                {
                    foreach (var line in File.ReadAllLines(ConfigFile, Encoding.UTF8))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        {
                            continue;
                        }

                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length != 2)
                        {
                            continue;
                        }

                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        switch (key)
                        {
                            case "YtDlpPath":
                                settings.YtDlpPath = value;
                                break;
                            case "FfmpegPath":
                                settings.FfmpegPath = value;
                                break;
                            case "FfprobePath":
                                settings.FfprobePath = value;
                                break;
                            case "OutputFolder":
                                settings.OutputFolder = value;
                                break;
                            case "CookiesPath":
                                settings.CookiesPath = value;
                                break;
                            case "Proxy":
                                settings.Proxy = value;
                                break;
                            case "FormatMode":
                                int mode;
                                if (int.TryParse(value, out mode))
                                {
                                    settings.FormatMode = mode;
                                }
                                break;
                            case "ThemeMode":
                                int theme;
                                if (int.TryParse(value, out theme))
                                {
                                    settings.ThemeMode = theme;
                                }
                                break;
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed settings and fall back to defaults.
            }

            return settings;
        }

        private static string GetDefaultOutputFolder()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrWhiteSpace(desktop))
            {
                return desktop;
            }

            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                return Path.Combine(profile, "Downloads");
            }

            return Directory.GetCurrentDirectory();
        }

        public void Save()
        {
            var lines = new List<string>
            {
                "# VideoDownloader settings",
                "YtDlpPath=" + YtDlpPath,
                "FfmpegPath=" + FfmpegPath,
                "FfprobePath=" + FfprobePath,
                "OutputFolder=" + OutputFolder,
                "CookiesPath=" + CookiesPath,
                "Proxy=" + Proxy,
                "FormatMode=" + FormatMode,
                "ThemeMode=" + ThemeMode
            };

            File.WriteAllLines(ConfigFile, lines, Encoding.UTF8);
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly AppSettings settings;
        private Process currentProcess;

        private TextBox urlTextBox;
        private TextBox outputFolderTextBox;
        private ComboBox formatComboBox;
        private TextBox ytDlpTextBox;
        private TextBox ffmpegTextBox;
        private TextBox ffprobeTextBox;
        private TextBox cookiesTextBox;
        private TextBox proxyTextBox;
        private TextBox logTextBox;
        private ComboBox themeComboBox;
        private PictureBox previewPictureBox;
        private Label previewStatusLabel;
        private Button downloadButton;
        private Button stopButton;

        public MainForm()
        {
            settings = AppSettings.Load();
            BuildUi();
            LoadSettingsIntoUi();
            ApplyTheme(settings.ThemeMode);
        }

        private void BuildUi()
        {
            Text = "视频下载器 - 基于 yt-dlp";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(980, 760);
            MinimumSize = new Size(820, 620);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 136f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

            Controls.Add(root);

            var downloadGroup = BuildDownloadGroup();
            var toolPathsGroup = BuildToolPathsGroup();
            var advancedGroup = BuildAdvancedGroup();
            var appearanceGroup = BuildAppearanceGroup();
            var logGroup = BuildLogGroup();
            var actionPanel = BuildActionPanel();
            var statusPanel = BuildStatusPanel();

            root.Controls.Add(downloadGroup, 0, 0);
            root.Controls.Add(toolPathsGroup, 1, 0);
            root.Controls.Add(advancedGroup, 0, 1);
            root.Controls.Add(appearanceGroup, 1, 1);
            root.Controls.Add(logGroup, 0, 3);
            root.SetColumnSpan(logGroup, 2);
            root.Controls.Add(actionPanel, 0, 2);
            root.SetColumnSpan(actionPanel, 2);
            root.Controls.Add(statusPanel, 0, 4);
            root.SetColumnSpan(statusPanel, 2);
        }

        private Control BuildDownloadGroup()
        {
            var group = new GroupBox
            {
                Text = "下载设置",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 8)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
                Padding = new Padding(8)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
            group.Controls.Add(table);

            table.Controls.Add(Label("视频地址"), 0, 0);
            urlTextBox = TextBox("");
            urlTextBox.Dock = DockStyle.Fill;
            urlTextBox.Margin = new Padding(0, 4, 8, 4);
            urlTextBox.ScrollBars = ScrollBars.Vertical;
            table.Controls.Add(urlTextBox, 1, 0);
            var pasteButton = Button("粘贴");
            pasteButton.Dock = DockStyle.Fill;
            pasteButton.Margin = new Padding(0, 4, 0, 4);
            pasteButton.Click += delegate { PasteClipboardInto(urlTextBox); };
            table.Controls.Add(pasteButton, 2, 0);

            table.Controls.Add(Label("保存目录"), 0, 1);
            outputFolderTextBox = TextBox(settings.OutputFolder);
            outputFolderTextBox.Dock = DockStyle.Fill;
            outputFolderTextBox.Margin = new Padding(0, 4, 8, 4);
            table.Controls.Add(outputFolderTextBox, 1, 1);
            var browseButton = Button("浏览");
            browseButton.Dock = DockStyle.Fill;
            browseButton.Margin = new Padding(0, 4, 0, 4);
            browseButton.Click += BrowseOutputFolder;
            table.Controls.Add(browseButton, 2, 1);

            table.Controls.Add(Label("下载格式"), 0, 2);
            formatComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 4)
            };
            formatComboBox.Items.Add("最佳 MP4（视频 + 音频，兼容性最好）");
            formatComboBox.Items.Add("最佳质量（自动选择视频和音频）");
            formatComboBox.Items.Add("仅提取音频 MP3");
            table.Controls.Add(formatComboBox, 1, 2);
            var formatsButton = Button("查看格式");
            formatsButton.Dock = DockStyle.Fill;
            formatsButton.Margin = new Padding(0, 4, 0, 4);
            formatsButton.Click += async delegate { await RunListFormats(); };
            table.Controls.Add(formatsButton, 2, 2);

            var previewPanel = BuildPreviewPanel();
            table.Controls.Add(previewPanel, 0, 3);
            table.SetColumnSpan(previewPanel, 3);

            return group;
        }

        private Control BuildPreviewPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 4, 0, 0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            previewPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(24, 24, 24),
                Margin = new Padding(0, 0, 12, 0)
            };
            panel.Controls.Add(previewPictureBox, 0, 0);

            var right = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            var previewUrlButton = Button("预览网页");
            previewUrlButton.Size = new Size(104, 34);
            previewUrlButton.Click += OpenPreviewUrl;
            right.Controls.Add(previewUrlButton);

            var thumbnailButton = Button("获取封面");
            thumbnailButton.Size = new Size(104, 34);
            thumbnailButton.Click += async delegate { await FetchThumbnailAsync(); };
            right.Controls.Add(thumbnailButton);

            previewStatusLabel = new Label
            {
                Text = "封面预览",
                AutoSize = true,
                Margin = new Padding(14, 10, 0, 0)
            };
            right.Controls.Add(previewStatusLabel);

            panel.Controls.Add(right, 1, 0);
            return panel;
        }

        private Control BuildToolPathsGroup()
        {
            var group = new GroupBox
            {
                Text = "程序路径设置",
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 8)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 7,
                Padding = new Padding(8)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90f));
            for (var i = 0; i < 7; i++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            }
            group.Controls.Add(table);

            AddPathRow(table, 0, "yt-dlp", out ytDlpTextBox);
            AddPathRow(table, 1, "ffmpeg", out ffmpegTextBox);
            AddPathRow(table, 2, "ffprobe", out ffprobeTextBox);
            AddPathRow(table, 3, "Cookies", out cookiesTextBox);

            var saveButton = Button("保存路径设置");
            saveButton.Dock = DockStyle.Fill;
            saveButton.Margin = new Padding(0, 2, 0, 0);
            saveButton.Click += delegate { SaveSettingsFromUi(); };
            table.Controls.Add(saveButton, 1, 4);

            var openFolderButton = Button("打开目录");
            openFolderButton.Dock = DockStyle.Fill;
            openFolderButton.Margin = new Padding(6, 2, 0, 0);
            openFolderButton.Click += delegate
            {
                var path = ytDlpTextBox.Text;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                else
                {
                    MessageBox.Show(this, "请先设置有效的 yt-dlp.exe 路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            table.Controls.Add(openFolderButton, 2, 4);

            var resetButton = Button("恢复默认路径");
            resetButton.Dock = DockStyle.Fill;
            resetButton.Margin = new Padding(0, 2, 0, 2);
            resetButton.Click += delegate { ResetPathFields(); };
            table.Controls.Add(resetButton, 1, 5);
            table.SetColumnSpan(resetButton, 2);

            var verifyButton = Button("测试工具路径");
            verifyButton.Dock = DockStyle.Fill;
            verifyButton.Margin = new Padding(0, 2, 0, 2);
            verifyButton.Click += delegate { VerifyToolPaths(); };
            table.Controls.Add(verifyButton, 1, 6);
            table.SetColumnSpan(verifyButton, 2);

            return group;
        }

        private void AddPathRow(TableLayoutPanel table, int row, string label, out TextBox textBox)
        {
            table.Controls.Add(Label(label), 0, row);
            var box = TextBox(string.Empty);
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(0, 3, 4, 3);
            table.Controls.Add(box, 1, row);

            var browseButton = Button("浏览");
            browseButton.Dock = DockStyle.Fill;
            browseButton.Margin = new Padding(0, 3, 0, 3);
            browseButton.Click += delegate { BrowseFile(box, "选择 " + label); };
            table.Controls.Add(browseButton, 2, row);

            textBox = box;
        }

        private void ResetPathFields()
        {
            ytDlpTextBox.Text = DefaultYtDlp;
            ffmpegTextBox.Text = DefaultFfmpeg;
            ffprobeTextBox.Text = DefaultFfprobe;
            SaveSettingsFromUi();
        }

        private void VerifyToolPaths()
        {
            var missing = new List<string>();
            CheckPath(ytDlpTextBox, "yt-dlp.exe", missing);
            CheckPath(ffmpegTextBox, "ffmpeg.exe", missing);
            CheckPath(ffprobeTextBox, "ffprobe.exe", missing);

            if (missing.Count == 0)
            {
                MessageBox.Show(this, "三个工具路径均可用。", "检查结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "以下路径不存在：\n\n" + string.Join(Environment.NewLine, missing), "检查结果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CheckPath(TextBox textBox, string name, List<string> missing)
        {
            var path = textBox.Text.Trim();
            if (!File.Exists(path))
            {
                missing.Add(name + "：" + path);
            }
        }

        private Control BuildAdvancedGroup()
        {
            var group = new GroupBox
            {
                Text = "网络与输出选项",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 8)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(8)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            group.Controls.Add(table);

            table.Controls.Add(Label("代理地址（可留空）"), 0, 0);
            proxyTextBox = TextBox(string.Empty);
            proxyTextBox.Dock = DockStyle.Fill;
            proxyTextBox.Margin = new Padding(0, 5, 8, 5);
            table.Controls.Add(proxyTextBox, 1, 0);
            table.SetColumnSpan(proxyTextBox, 2);

            var overwriteCheck = new CheckBox
            {
                Text = "已存在时覆盖文件",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0)
            };
            table.Controls.Add(overwriteCheck, 0, 1);

            var hint = Label("文件名会自动生成，无需额外填写。");
            hint.ForeColor = SystemColors.GrayText;
            hint.Margin = new Padding(8, 10, 0, 0);
            table.Controls.Add(hint, 1, 1);
            table.SetColumnSpan(hint, 2);

            overwriteCheck.CheckedChanged += delegate { overwriteEnabled = overwriteCheck.Checked; };
            return group;
        }

        private bool overwriteEnabled = true;

        private Control BuildAppearanceGroup()
        {
            var group = new GroupBox
            {
                Text = "界面外观",
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 8, 0, 8)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(8)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            group.Controls.Add(table);

            table.Controls.Add(Label("皮肤"), 0, 0);
            themeComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5)
            };
            themeComboBox.Items.Add("深空黑");
            themeComboBox.Items.Add("浅色");
            themeComboBox.Items.Add("经典蓝");
            themeComboBox.SelectedIndexChanged += delegate
            {
                var mode = themeComboBox.SelectedIndex;
                ApplyTheme(mode);
                settings.ThemeMode = mode;
                try
                {
                    settings.Save();
                }
                catch
                {
                    // A read-only location should not prevent the app from running.
                }
            };
            table.Controls.Add(themeComboBox, 1, 0);

            var hint = Label("切换后立即生效并自动保存");
            hint.ForeColor = SystemColors.GrayText;
            hint.Margin = new Padding(0, 8, 0, 0);
            table.Controls.Add(hint, 1, 1);

            return group;
        }

        private Control BuildLogGroup()
        {
            var group = new GroupBox
            {
                Text = "运行日志",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8)
            };

            logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9f),
                Dock = DockStyle.Fill
            };
            group.Controls.Add(logTextBox);
            return group;
        }

        private Control BuildActionPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0)
            };

            downloadButton = Button("开始下载");
            downloadButton.Size = new Size(120, 42);
            downloadButton.Tag = "primary";
            downloadButton.BackColor = Color.FromArgb(52, 120, 246);
            downloadButton.ForeColor = Color.White;
            downloadButton.FlatStyle = FlatStyle.Flat;
            downloadButton.FlatAppearance.BorderSize = 0;
            downloadButton.Click += async delegate { await StartDownloadAsync(); };

            stopButton = Button("停止");
            stopButton.Size = new Size(92, 42);
            stopButton.Enabled = false;
            stopButton.Click += StopDownload;

            var clearButton = Button("清空日志");
            clearButton.Size = new Size(92, 42);
            clearButton.Click += delegate { logTextBox.Clear(); };

            var openDirButton = Button("打开下载目录");
            openDirButton.Size = new Size(118, 42);
            openDirButton.Click += delegate
            {
                var folder = outputFolderTextBox.Text;
                if (Directory.Exists(folder))
                {
                    Process.Start("explorer.exe", "\"" + folder + "\"");
                }
                else
                {
                    MessageBox.Show(this, "保存目录不存在，请先创建或重新选择。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            panel.Controls.Add(downloadButton);
            panel.Controls.Add(stopButton);
            panel.Controls.Add(clearButton);
            panel.Controls.Add(openDirButton);
            return panel;
        }

        private Control BuildStatusPanel()
        {
            var label = Label("就绪");
            label.Dock = DockStyle.Fill;
            label.ForeColor = SystemColors.GrayText;
            statusLabel = label;
            return label;
        }

        private Label statusLabel;

        private void LoadSettingsIntoUi()
        {
            ytDlpTextBox.Text = settings.YtDlpPath;
            ffmpegTextBox.Text = settings.FfmpegPath;
            ffprobeTextBox.Text = settings.FfprobePath;
            outputFolderTextBox.Text = settings.OutputFolder;
            cookiesTextBox.Text = settings.CookiesPath;
            proxyTextBox.Text = settings.Proxy;
            formatComboBox.SelectedIndex = settings.FormatMode >= 0 && settings.FormatMode < formatComboBox.Items.Count
                ? settings.FormatMode
                : 0;
            themeComboBox.SelectedIndex = settings.ThemeMode >= 0 && settings.ThemeMode < themeComboBox.Items.Count
                ? settings.ThemeMode
                : 0;
        }

        private void SaveSettingsFromUi()
        {
            settings.YtDlpPath = ytDlpTextBox.Text.Trim();
            settings.FfmpegPath = ffmpegTextBox.Text.Trim();
            settings.FfprobePath = ffprobeTextBox.Text.Trim();
            settings.OutputFolder = outputFolderTextBox.Text.Trim();
            settings.CookiesPath = cookiesTextBox.Text.Trim();
            settings.Proxy = proxyTextBox.Text.Trim();
            settings.FormatMode = formatComboBox.SelectedIndex;
            settings.ThemeMode = themeComboBox.SelectedIndex;
            try
            {
                settings.Save();
                AppendLog("设置已保存到 " + AppSettings.ConfigFile);
                MessageBox.Show(this, "设置已保存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "设置保存失败：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenPreviewUrl(object sender, EventArgs e)
        {
            var url = urlTextBox.Text.Trim();
            if (!ValidateUrl(url))
            {
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开预览页面：\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task FetchThumbnailAsync()
        {
            var url = urlTextBox.Text.Trim();
            if (!ValidateUrl(url))
            {
                return;
            }

            var ytDlp = settings.YtDlpPath;
            if (!File.Exists(ytDlp))
            {
                ShowMissingTool("yt-dlp.exe", ytDlp);
                return;
            }

            SetPreviewStatus("正在获取封面...");
            try
            {
                var output = await Task.Run(delegate
                {
                    return RunProcessCapture(ytDlp, BuildThumbnailArgs(url));
                });

                var thumbnailUrl = ExtractThumbnailUrl(output);
                if (thumbnailUrl == null)
                {
                    SetPreviewStatus("未找到封面");
                    return;
                }

                var bytes = await Task.Run(delegate
                {
                    using (var client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "Mozilla/5.0");
                        return client.DownloadData(thumbnailUrl);
                    }
                });

                using (var stream = new MemoryStream(bytes))
                using (var image = Image.FromStream(stream))
                {
                    if (previewPictureBox.InvokeRequired)
                    {
                        previewPictureBox.BeginInvoke((Action)(delegate
                        {
                            var old = previewPictureBox.Image;
                            previewPictureBox.Image = new Bitmap(image);
                            if (old != null)
                            {
                                old.Dispose();
                            }
                        }));
                    }
                    else
                    {
                        var old = previewPictureBox.Image;
                        previewPictureBox.Image = new Bitmap(image);
                        if (old != null)
                        {
                            old.Dispose();
                        }
                    }
                }

                SetPreviewStatus("封面已加载");
            }
            catch (Exception ex)
            {
                SetPreviewStatus("封面获取失败");
                AppendLog("获取封面失败：" + ex.Message);
            }
        }

        private string BuildThumbnailArgs(string url)
        {
            var args = new List<string>
            {
                "--encoding",
                "utf-8",
                "--no-playlist",
                "--get-thumbnail"
            };

            if (!string.IsNullOrWhiteSpace(settings.CookiesPath) && File.Exists(settings.CookiesPath))
            {
                args.Add("--cookies");
                args.Add(settings.CookiesPath);
            }

            if (!string.IsNullOrWhiteSpace(settings.Proxy))
            {
                args.Add("--proxy");
                args.Add(settings.Proxy);
            }

            args.Add(url);
            return string.Join(" ", QuoteAll(args));
        }

        private static string RunProcessCapture(string fileName, string arguments)
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(info))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return stdout + Environment.NewLine + stderr;
            }
        }

        private static string ExtractThumbnailUrl(string output)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return line;
                }
            }

            return null;
        }

        private void SetPreviewStatus(string text)
        {
            if (previewStatusLabel.InvokeRequired)
            {
                previewStatusLabel.BeginInvoke((Action)(delegate { SetPreviewStatus(text); }));
                return;
            }

            previewStatusLabel.Text = text;
        }

        private void ApplyTheme(int mode)
        {
            var palette = ThemePalette.Get(mode);
            BackColor = palette.Form;
            ApplyThemeTo(this, palette);
        }

        private static void ApplyThemeTo(Control control, ThemePalette palette)
        {
            foreach (Control child in control.Controls)
            {
                if (child is GroupBox)
                {
                    child.BackColor = palette.Panel;
                    child.ForeColor = palette.Text;
                }
                else if (child is Label)
                {
                    child.BackColor = palette.Panel;
                    child.ForeColor = palette.Text;
                }
                else if (child is TextBox)
                {
                    var textBox = child as TextBox;
                    if (textBox.ReadOnly && textBox.Multiline)
                    {
                        child.BackColor = palette.LogBack;
                        child.ForeColor = palette.LogFore;
                    }
                    else
                    {
                        child.BackColor = palette.Input;
                        child.ForeColor = palette.Text;
                    }
                }
                else if (child is ComboBox)
                {
                    child.BackColor = palette.Input;
                    child.ForeColor = palette.Text;
                }
                else if (child is CheckBox)
                {
                    child.BackColor = palette.Panel;
                    child.ForeColor = palette.Text;
                }
                else if (child is PictureBox)
                {
                    child.BackColor = palette.Panel;
                }
                else if (child is TableLayoutPanel || child is Panel || child is FlowLayoutPanel)
                {
                    child.BackColor = Color.Transparent;
                }

                if (child is Button)
                {
                    var button = child as Button;
                    if (button.Tag as string == "primary")
                    {
                        button.BackColor = palette.Accent;
                        button.ForeColor = Color.White;
                    }
                    else
                    {
                        button.BackColor = palette.Button;
                        button.ForeColor = palette.ButtonText;
                    }

                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                }

                ApplyThemeTo(child, palette);
            }
        }

        private void BrowseOutputFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择视频保存目录";
                dialog.SelectedPath = outputFolderTextBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    outputFolderTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void BrowseFile(TextBox textBox, string title)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                if (textBox == cookiesTextBox)
                {
                    dialog.Filter = "Cookies 文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                }

                if (File.Exists(textBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(textBox.Text);
                    dialog.FileName = Path.GetFileName(textBox.Text);
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    textBox.Text = dialog.FileName;
                }
            }
        }

        private void PasteClipboardInto(TextBox textBox)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    textBox.Text = Clipboard.GetText().Trim();
                }
            }
            catch
            {
                // Clipboard access can fail if it is locked by another process.
            }
        }

        private async Task RunListFormats()
        {
            var url = urlTextBox.Text.Trim();
            if (!ValidateUrl(url))
            {
                return;
            }

            var ytDlp = settings.YtDlpPath;
            if (!File.Exists(ytDlp))
            {
                ShowMissingTool("yt-dlp.exe", ytDlp);
                return;
            }

            await RunToolAsync(
                ytDlp,
                BuildListFormatsArguments(url),
                "查看格式",
                false);
        }

        private async Task StartDownloadAsync()
        {
            var url = urlTextBox.Text.Trim();
            if (!ValidateUrl(url))
            {
                return;
            }

            var ytDlp = settings.YtDlpPath;
            if (!File.Exists(ytDlp))
            {
                ShowMissingTool("yt-dlp.exe", ytDlp);
                return;
            }

            var outputFolder = outputFolderTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                MessageBox.Show(this, "请选择保存目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(outputFolder))
            {
                var result = MessageBox.Show(
                    this,
                    "保存目录不存在，是否现在创建？\n\n" + outputFolder,
                    "提示",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        Directory.CreateDirectory(outputFolder);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "无法创建目录：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            await RunToolAsync(
                ytDlp,
                BuildDownloadArguments(url, outputFolder),
                "下载",
                true);
        }

        private bool ValidateUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(this, "请先粘贴视频地址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "视频地址必须以 http:// 或 https:// 开头。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private string BuildListFormatsArguments(string url)
        {
            var args = new List<string> { "--encoding", "utf-8", "--ffmpeg-location", GetFfmpegDirectory(), "-F", url };
            return string.Join(" ", QuoteAll(args));
        }

        private string BuildDownloadArguments(string url, string outputFolder)
        {
            var args = new List<string>
            {
                "--encoding",
                "utf-8",
                "--ffmpeg-location",
                GetFfmpegDirectory(),
                "--newline",
                "--no-playlist"
            };

            if (overwriteEnabled)
            {
                args.Add("--force-overwrites");
            }

            if (!string.IsNullOrWhiteSpace(settings.CookiesPath) && File.Exists(settings.CookiesPath))
            {
                args.Add("--cookies");
                args.Add(settings.CookiesPath);
            }

            if (!string.IsNullOrWhiteSpace(settings.Proxy))
            {
                args.Add("--proxy");
                args.Add(settings.Proxy);
            }

            switch (formatComboBox.SelectedIndex)
            {
                case 0:
                    args.Add("-t");
                    args.Add("mp4");
                    break;
                case 2:
                    args.Add("-x");
                    args.Add("--audio-format");
                    args.Add("mp3");
                    args.Add("--audio-quality");
                    args.Add("0");
                    break;
                default:
                    args.Add("-f");
                    args.Add("bestvideo*+bestaudio/best");
                    break;
            }

            var template = Path.Combine(outputFolder, "%(title)s [%(id)s].%(ext)s");
            args.Add("-o");
            args.Add(template);
            args.Add(url);

            return string.Join(" ", QuoteAll(args));
        }

        private string GetFfmpegDirectory()
        {
            var ffmpeg = settings.FfmpegPath;
            if (!string.IsNullOrWhiteSpace(ffmpeg) && File.Exists(ffmpeg))
            {
                return Path.GetDirectoryName(ffmpeg);
            }

            if (!string.IsNullOrWhiteSpace(settings.FfprobePath) && File.Exists(settings.FfprobePath))
            {
                return Path.GetDirectoryName(settings.FfprobePath);
            }

            return string.Empty;
        }

        private async Task RunToolAsync(string fileName, string arguments, string actionName, bool disableDownload)
        {
            SetBusy(true, disableDownload, "正在" + actionName + "...");
            AppendLog("开始" + actionName + "：");
            AppendLog(fileName + " " + arguments);
            AppendLog("");

            currentProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            currentProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    AppendLogSafe(e.Data);
                }
            };
            currentProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    AppendLogSafe(e.Data);
                }
            };

            try
            {
                await Task.Run(delegate
                {
                    currentProcess.Start();
                    currentProcess.BeginOutputReadLine();
                    currentProcess.BeginErrorReadLine();
                    currentProcess.StandardInput.Close();
                    currentProcess.WaitForExit();
                });

                var exitCode = currentProcess.ExitCode;
                if (exitCode == 0)
                {
                    AppendLog("");
                    AppendLog(actionName + "完成。");
                    statusLabel.Text = actionName + "完成";
                }
                else
                {
                    AppendLog("");
                    AppendLog(actionName + "失败，yt-dlp 退出代码：" + exitCode);
                    statusLabel.Text = actionName + "失败";
                }
            }
            catch (Exception ex)
            {
                AppendLog("");
                AppendLog(actionName + "发生错误：" + ex.Message);
                statusLabel.Text = actionName + "错误";
            }
            finally
            {
                SetBusy(false, disableDownload, "就绪");
                currentProcess = null;
            }
        }

        private void SetBusy(bool busy, bool disableDownload, string status)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(delegate { SetBusy(busy, disableDownload, status); }));
                return;
            }

            downloadButton.Enabled = !busy;
            if (disableDownload)
            {
                stopButton.Enabled = busy;
            }

            statusLabel.Text = status;
        }

        private void StopDownload(object sender, EventArgs e)
        {
            try
            {
                if (currentProcess != null && !currentProcess.HasExited)
                {
                    currentProcess.Kill();
                    AppendLog("已请求停止下载。");
                    statusLabel.Text = "已停止";
                }
            }
            catch (Exception ex)
            {
                AppendLog("停止下载失败：" + ex.Message);
            }
        }

        private void AppendLog(string text)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.BeginInvoke((Action)(delegate { AppendLog(text); }));
                return;
            }

            logTextBox.AppendText(text + Environment.NewLine);
        }

        private void AppendLogSafe(string text)
        {
            if (logTextBox.InvokeRequired)
            {
                try
                {
                    logTextBox.BeginInvoke((Action)(delegate { AppendLog(text); }));
                }
                catch
                {
                    // The form may already be closing.
                }
                return;
            }

            logTextBox.AppendText(text + Environment.NewLine);
        }

        private void ShowMissingTool(string toolName, string path)
        {
            MessageBox.Show(
                this,
                toolName + " 不存在，请在右侧“程序路径设置”中选择正确的文件。\n\n当前路径：\n" + path,
                "找不到工具",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static string[] QuoteAll(IEnumerable<string> args)
        {
            var quoted = new List<string>();
            foreach (var arg in args)
            {
                quoted.Add(QuoteArg(arg));
            }
            return quoted.ToArray();
        }

        private static string QuoteArg(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return "\"\"";
            }

            if (arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                return arg;
            }

            var builder = new StringBuilder();
            builder.Append('"');
            var backslashes = 0;

            foreach (var c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    builder.Append('\\', (backslashes * 2) + 1);
                    builder.Append('"');
                    backslashes = 0;
                }
                else
                {
                    builder.Append('\\', backslashes);
                    backslashes = 0;
                    builder.Append(c);
                }
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        private static Label Label(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 8, 4)
            };
        }

        private static TextBox TextBox(string text)
        {
            return new TextBox
            {
                Text = text,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private static Button Button(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = false
            };
        }
    }

    internal sealed class ThemePalette
    {
        public Color Form { get; private set; }
        public Color Panel { get; private set; }
        public Color Text { get; private set; }
        public Color Input { get; private set; }
        public Color Accent { get; private set; }
        public Color Button { get; private set; }
        public Color ButtonText { get; private set; }
        public Color LogBack { get; private set; }
        public Color LogFore { get; private set; }

        private ThemePalette(
            Color form,
            Color panel,
            Color text,
            Color input,
            Color accent,
            Color button,
            Color buttonText,
            Color logBack,
            Color logFore)
        {
            Form = form;
            Panel = panel;
            Text = text;
            Input = input;
            Accent = accent;
            Button = button;
            ButtonText = buttonText;
            LogBack = logBack;
            LogFore = logFore;
        }

        public static ThemePalette Get(int mode)
        {
            switch (mode)
            {
                case 1:
                    return new ThemePalette(
                        Color.FromArgb(238, 241, 246),
                        Color.White,
                        Color.FromArgb(33, 37, 43),
                        Color.White,
                        Color.FromArgb(47, 111, 219),
                        Color.FromArgb(227, 232, 240),
                        Color.FromArgb(33, 37, 43),
                        Color.FromArgb(29, 34, 42),
                        Color.FromArgb(205, 211, 223));
                case 2:
                    return new ThemePalette(
                        Color.FromArgb(237, 243, 251),
                        Color.White,
                        Color.FromArgb(28, 43, 69),
                        Color.White,
                        Color.FromArgb(37, 99, 217),
                        Color.FromArgb(230, 238, 250),
                        Color.FromArgb(28, 43, 69),
                        Color.FromArgb(15, 23, 42),
                        Color.FromArgb(205, 211, 223));
                default:
                    return new ThemePalette(
                        Color.FromArgb(23, 24, 28),
                        Color.FromArgb(34, 37, 44),
                        Color.FromArgb(230, 232, 238),
                        Color.FromArgb(45, 48, 56),
                        Color.FromArgb(79, 124, 255),
                        Color.FromArgb(50, 53, 61),
                        Color.FromArgb(230, 232, 238),
                        Color.FromArgb(19, 20, 24),
                        Color.FromArgb(205, 211, 223));
            }
        }
    }

    internal static class TextBoxExtensions
    {
        public static void PlaceholderTextCompat(this TextBox textBox, string placeholder)
        {
            // Placeholder is intentionally visual-only on systems where TextBox doesn't support it.
            textBox.Tag = placeholder;
            textBox.Enter += delegate { if (textBox.Text == placeholder) { textBox.Text = string.Empty; } };
            textBox.Leave += delegate { if (string.IsNullOrEmpty(textBox.Text)) { textBox.Text = placeholder; } };
        }
    }
}
