// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace VideoDownloader
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings settings;
        private Process currentProcess;
        private bool isLoading;
        private bool isRunning;
        private bool isPaused;
        private bool stopRequested;
        private string lastCompletedPath;
        private string finalPathCandidate;
        private string lastDownloadDestination;
        private string activeOutputFolder;
        private DateTime downloadStartedAt;
        private IntPtr currentJobHandle;
        private double lastLoggedPercent = -1;
        private bool finalOutputSeen;
        private int currentPlaylistIndex;
        private int currentPlaylistTotal;
        private string lastFallbackLogText;
        private bool cookieErrorHintLogged;

        internal MainWindow(AppSettings settings)
        {
            this.settings = settings;
            isLoading = true;
            InitializeComponent();
            LoadSettingsIntoUi();
            ThemeManager.ApplyTheme(settings.ThemeMode);
            isLoading = false;
        }

        private void LoadSettingsIntoUi()
        {
            YtDlpTextBox.Text = settings.YtDlpPath;
            FfmpegTextBox.Text = settings.FfmpegPath;
            FfprobeTextBox.Text = settings.FfprobePath;
            VideoOutputTextBox.Text = settings.OutputFolder;
            MusicOutputTextBox.Text = settings.MusicOutputFolder;
            CookiesTextBox.Text = settings.CookiesPath;
            ProxyTextBox.Text = settings.Proxy;
            VideoFormatComboBox.SelectedIndex = ClampIndex(settings.FormatMode, VideoFormatComboBox.Items.Count);
            MusicFormatComboBox.SelectedIndex = ClampIndex(settings.MusicFormat, MusicFormatComboBox.Items.Count);
            ThemeComboBox.SelectedIndex = ClampIndex(settings.ThemeMode, ThemeComboBox.Items.Count);
            MusicEmbedCheckBox.IsChecked = settings.EmbedMetadata;
            ImpersonateComboBox.SelectedIndex = MapImpersonateToIndex(settings.ImpersonateClient);
            MusicPlaylistCheckBox.IsChecked = settings.MusicPlaylistMode;
            MusicPlaylistRangeTextBox.Text = settings.MusicPlaylistRange;
            CookiesSourceComboBox.SelectedIndex = MapCookiesSourceToIndex(settings.CookiesSource);
        }

        private static int MapCookiesSourceToIndex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "chrome":
                    return 1;
                case "edge":
                    return 2;
                case "firefox":
                    return 3;
                case "none":
                    return 4;
                default:
                    return 0;
            }
        }

        private string GetCookiesSource()
        {
            switch (CookiesSourceComboBox.SelectedIndex)
            {
                case 1:
                    return "chrome";
                case 2:
                    return "edge";
                case 3:
                    return "firefox";
                case 4:
                    return "none";
                default:
                    return "file";
            }
        }

        private static int MapImpersonateToIndex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var mode = value.Trim().ToLowerInvariant();
            if (mode == "safari") return 1;
            if (mode == "chrome") return 2;
            if (mode == "edge") return 3;
            if (mode == "firefox") return 4;
            if (mode == "none") return 5;
            return 0;
        }

        private string GetImpersonateMode()
        {
            var index = ImpersonateComboBox.SelectedIndex;
            if (index == 1) return "safari";
            if (index == 2) return "chrome";
            if (index == 3) return "edge";
            if (index == 4) return "firefox";
            if (index == 5) return "none";
            return "auto";
        }

        private static int ClampIndex(int value, int count)
        {
            if (value >= 0 && value < count)
            {
                return value;
            }

            return 0;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUi(true);
        }

        private void SaveSettingsFromUi(bool showMessage)
        {
            settings.YtDlpPath = YtDlpTextBox.Text.Trim();
            settings.FfmpegPath = FfmpegTextBox.Text.Trim();
            settings.FfprobePath = FfprobeTextBox.Text.Trim();
            settings.OutputFolder = VideoOutputTextBox.Text.Trim();
            settings.MusicOutputFolder = MusicOutputTextBox.Text.Trim();
            settings.CookiesPath = CookiesTextBox.Text.Trim();
            settings.Proxy = ProxyTextBox.Text.Trim();
            settings.FormatMode = VideoFormatComboBox.SelectedIndex;
            settings.MusicFormat = MusicFormatComboBox.SelectedIndex;
            settings.ThemeMode = ThemeComboBox.SelectedIndex;
            settings.EmbedMetadata = MusicEmbedCheckBox.IsChecked == true;
            settings.ImpersonateClient = GetImpersonateMode();
            settings.MusicPlaylistMode = MusicPlaylistCheckBox.IsChecked == true;
            settings.MusicPlaylistRange = MusicPlaylistRangeTextBox.Text.Trim();
            settings.MusicPlaylistSubfolder = true;
            settings.CookiesSource = GetCookiesSource();

            try
            {
                settings.Save();
                AppendLog("设置已保存到 " + AppSettings.ConfigFile);
                SetStatus("设置已保存");
                if (showMessage)
                {
                    System.Windows.MessageBox.Show(this, "设置已保存。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog("设置保存失败：" + ex.Message);
                SetStatus("设置保存失败");
                if (showMessage)
                {
                    System.Windows.MessageBox.Show(this, "设置保存失败：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoading)
            {
                return;
            }

            var mode = ThemeComboBox.SelectedIndex < 0 ? 0 : ThemeComboBox.SelectedIndex;
            ThemeManager.ApplyTheme(mode);
            settings.ThemeMode = mode;
            TrySaveSilent();
        }

        private void TrySaveSilent()
        {
            try
            {
                settings.Save();
            }
            catch
            {
                // A read-only location should not prevent the app from running.
            }
        }

        private void BrowseVideoOutput_Click(object sender, RoutedEventArgs e)
        {
            BrowseFolder(VideoOutputTextBox, "选择视频保存目录");
        }

        private void BrowseMusicOutput_Click(object sender, RoutedEventArgs e)
        {
            BrowseFolder(MusicOutputTextBox, "选择音乐保存目录");
        }

        private void BrowseYtDlp_Click(object sender, RoutedEventArgs e)
        {
            BrowseFile(YtDlpTextBox, "选择 yt-dlp.exe", "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*");
        }

        private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            BrowseFile(FfmpegTextBox, "选择 ffmpeg.exe", "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*");
        }

        private void BrowseFfprobe_Click(object sender, RoutedEventArgs e)
        {
            BrowseFile(FfprobeTextBox, "选择 ffprobe.exe", "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*");
        }

        private void BrowseCookies_Click(object sender, RoutedEventArgs e)
        {
            BrowseFile(CookiesTextBox, "选择 Cookies 文件", "Cookies 文件 (*.txt)|*.txt|所有文件 (*.*)|*.*");
        }

        private void BrowseFolder(TextBox textBox, string description)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.SelectedPath = textBox.Text;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    textBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void BrowseFile(TextBox textBox, string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter
            };

            if (File.Exists(textBox.Text))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(textBox.Text);
                dialog.FileName = Path.GetFileName(textBox.Text);
            }

            if (dialog.ShowDialog(this) == true)
            {
                textBox.Text = dialog.FileName;
            }
        }

        private void PasteVideo_Click(object sender, RoutedEventArgs e)
        {
            PasteClipboardInto(VideoUrlTextBox);
        }

        private void PasteMusic_Click(object sender, RoutedEventArgs e)
        {
            PasteClipboardInto(MusicUrlTextBox);
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

        private void OpenVideoPreview_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(VideoUrlTextBox.Text);
        }

        private void OpenUrl(string url)
        {
            if (!ValidateUrl(url, "视频地址"))
            {
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, "无法打开预览页面：\n" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void FetchVideoThumbnail_Click(object sender, RoutedEventArgs e)
        {
            await FetchThumbnailAsync();
        }

        private async Task FetchThumbnailAsync()
        {
            var url = VideoUrlTextBox.Text.Trim();
            if (!ValidateUrl(url, "视频地址"))
            {
                return;
            }

            var ytDlp = YtDlpTextBox.Text.Trim();
            if (!File.Exists(ytDlp))
            {
                ShowMissingTool("yt-dlp.exe", ytDlp);
                return;
            }

            SetStatus("正在获取封面...");
            try
            {
                var arguments = DownloaderService.BuildThumbnailArguments(url, settings.CookiesPath, GetCookiesSource(), settings.Proxy, GetImpersonateMode());
                var output = await Task.Run(() => DownloaderService.RunProcessCapture(ytDlp, arguments));
                var thumbnailUrl = ExtractThumbnailUrl(output);
                if (thumbnailUrl == null)
                {
                    VideoPreviewStatusText.Text = "未找到封面";
                    SetStatus("未找到封面");
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
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    VideoPreviewImage.Source = bitmap;
                }

                VideoPreviewStatusText.Text = "封面已加载";
                SetStatus("封面已加载");
            }
            catch (Exception ex)
            {
                VideoPreviewStatusText.Text = "封面获取失败";
                SetStatus("封面获取失败");
                AppendLog("获取封面失败：" + ex.Message);
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

        private async void ListVideoFormats_Click(object sender, RoutedEventArgs e)
        {
            var url = VideoUrlTextBox.Text.Trim();
            if (!ValidateUrl(url, "视频地址"))
            {
                return;
            }

            var ytDlp = YtDlpTextBox.Text.Trim();
            if (!File.Exists(ytDlp))
            {
                ShowMissingTool("yt-dlp.exe", ytDlp);
                return;
            }

            var ffmpegDir = DownloaderService.GetFfmpegDirectory(FfmpegTextBox.Text.Trim(), FfprobeTextBox.Text.Trim());
            var arguments = DownloaderService.BuildListFormatsArguments(url, ffmpegDir, settings.CookiesPath, GetCookiesSource(), settings.Proxy, GetImpersonateMode());
            await RunToolAsync(ytDlp, arguments, "查看格式", false);
        }

        private async void StartVideoDownload_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;
            SetBusy(true, "正在准备下载...");
            var toolStarted = false;
            try
            {
                var url = VideoUrlTextBox.Text.Trim();
                if (!ValidateUrl(url, "视频地址"))
                {
                    return;
                }

                var outputFolder = PrepareOutputFolder(VideoOutputTextBox, "视频");
                if (outputFolder == null)
                {
                    return;
                }

                var ytDlp = YtDlpTextBox.Text.Trim();
                if (!File.Exists(ytDlp))
                {
                    ShowMissingTool("yt-dlp.exe", ytDlp);
                    return;
                }

                var ffmpegDir = DownloaderService.GetFfmpegDirectory(FfmpegTextBox.Text.Trim(), FfprobeTextBox.Text.Trim());
                var arguments = DownloaderService.BuildVideoDownloadArguments(
                    url,
                    outputFolder,
                    VideoFormatComboBox.SelectedIndex,
                    VideoOverwriteCheckBox.IsChecked == true,
                    settings.CookiesPath,
                    GetCookiesSource(),
                    settings.Proxy,
                    ffmpegDir,
                    GetImpersonateMode());

                activeOutputFolder = outputFolder;
                toolStarted = true;
                await RunToolAsync(ytDlp, arguments, "视频下载", true);
            }
            finally
            {
                if (!toolStarted)
                {
                    isRunning = false;
                    SetBusy(false, "就绪");
                }
            }
        }

        private async void StartMusicDownload_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;
            SetBusy(true, "正在准备下载...");
            var toolStarted = false;
            try
            {
                var url = MusicUrlTextBox.Text.Trim();
                if (!ValidateUrl(url, "音乐地址"))
                {
                    return;
                }

                var range = MusicPlaylistRangeTextBox.Text.Trim();
                var playlistMode = MusicPlaylistCheckBox.IsChecked == true ||
                                   !string.IsNullOrWhiteSpace(range) ||
                                   LooksLikePlaylistUrl(url);
                if (playlistMode)
                {
                    MusicPlaylistCheckBox.IsChecked = true;
                }

                var outputFolder = PrepareOutputFolder(MusicOutputTextBox, "音乐");
                if (outputFolder == null)
                {
                    return;
                }

                var ytDlp = YtDlpTextBox.Text.Trim();
                if (!File.Exists(ytDlp))
                {
                    ShowMissingTool("yt-dlp.exe", ytDlp);
                    return;
                }

                var ffmpeg = FfmpegTextBox.Text.Trim();
                if (!File.Exists(ffmpeg))
                {
                    ShowMissingTool("ffmpeg.exe", ffmpeg);
                    return;
                }

                var ffmpegDir = DownloaderService.GetFfmpegDirectory(ffmpeg, FfprobeTextBox.Text.Trim());
                var arguments = DownloaderService.BuildMusicDownloadArguments(
                    url,
                    outputFolder,
                    MusicFormatComboBox.SelectedIndex,
                    MusicEmbedCheckBox.IsChecked == true,
                    true,
                    settings.CookiesPath,
                    GetCookiesSource(),
                    settings.Proxy,
                    ffmpegDir,
                    GetImpersonateMode(),
                    playlistMode,
                    range,
                    true);

                activeOutputFolder = outputFolder;
                toolStarted = true;
                await RunToolAsync(ytDlp, arguments, "无损音乐下载", true);
            }
            finally
            {
                if (!toolStarted)
                {
                    isRunning = false;
                    SetBusy(false, "就绪");
                }
            }
        }

        private string PrepareOutputFolder(TextBox textBox, string label)
        {
            var folder = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder))
            {
                System.Windows.MessageBox.Show(this, "请选择" + label + "保存目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (Directory.Exists(folder))
            {
                return folder;
            }

            var result = System.Windows.MessageBox.Show(
                this,
                label + "保存目录不存在，是否现在创建？\n\n" + folder,
                "提示",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(folder);
                return folder;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, "无法创建目录：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private bool ValidateUrl(string url, string label)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                System.Windows.MessageBox.Show(this, "请先粘贴" + label + "。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show(this, label + "必须以 http:// 或 https:// 开头。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static bool LooksLikePlaylistUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            var lower = url.ToLowerInvariant();
            return lower.Contains("/playlist?list=") ||
                   lower.Contains("?list=") ||
                   lower.Contains("&list=");
        }

        private async Task RunToolAsync(string fileName, string arguments, string actionName, bool verifyAudio)
        {
            isRunning = true;
            lastCompletedPath = null;
            finalPathCandidate = null;
            lastDownloadDestination = null;
            downloadStartedAt = DateTime.Now;
            finalOutputSeen = false;
            currentPlaylistIndex = 0;
            currentPlaylistTotal = 0;
            cookieErrorHintLogged = false;
            isPaused = false;
            stopRequested = false;
            lastLoggedPercent = -1;
            currentJobHandle = ProcessControl.CreateJob();
            SetBusy(true, "正在" + actionName + "...");
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
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                }
            };

            currentProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ProcessOutputLine(e.Data);
                }
            };
            currentProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ProcessOutputLine(e.Data);
                }
            };

            try
            {
                await Task.Run(delegate
                {
                    currentProcess.Start();
                    if (currentJobHandle != IntPtr.Zero)
                    {
                        if (!ProcessControl.Assign(currentJobHandle, currentProcess))
                        {
                            AppendLog("警告：无法将进程加入任务组，暂停/停止可能只作用于主进程。");
                        }
                    }
                    currentProcess.BeginOutputReadLine();
                    currentProcess.BeginErrorReadLine();
                    currentProcess.StandardInput.Close();
                    currentProcess.WaitForExit();
                });

                var exitCode = currentProcess.ExitCode;
                if (stopRequested)
                {
                    AppendLog("");
                    AppendLog(actionName + "已被停止。");
                    SetStatus("已停止");
                }
                else if (exitCode == 0)
                {
                    AppendLog("");
                    AppendLog(actionName + "完成。");
                    if (verifyAudio)
                    {
                        VerifyAudioStream();
                    }
                    else
                    {
                        SetStatus(actionName + "完成");
                    }
                }
                else if (exitCode != 0 && verifyAudio && finalOutputSeen)
                {
                    AppendLog("");
                    AppendLog(actionName + "返回非零退出码，但最终文件已生成，正在校验。");
                    VerifyAudioStream();
                }
                else
                {
                    AppendLog("");
                    AppendLog(actionName + "失败，yt-dlp 退出代码：" + exitCode);
                    SetStatus(actionName + "失败");
                }
            }
            catch (Exception ex)
            {
                AppendLog("");
                if (stopRequested)
                {
                    AppendLog(actionName + "已被停止。");
                    SetStatus("已停止");
                }
                else
                {
                    AppendLog(actionName + "发生错误：" + ex.Message);
                    SetStatus(actionName + "错误");
                }
            }
            finally
            {
                if (currentJobHandle != IntPtr.Zero)
                {
                    ProcessControl.Close(currentJobHandle);
                    currentJobHandle = IntPtr.Zero;
                }
                SetBusy(false, "就绪");
                currentProcess = null;
                isRunning = false;
                isPaused = false;
                stopRequested = false;
            }
        }

        private void ProcessOutputLine(string line)
        {
            if (!cookieErrorHintLogged)
            {
                if (line.IndexOf("Sign in to confirm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cookieErrorHintLogged = true;
                    AppendLog("提示：YouTube 要求登录验证。请在“设置”页把 Cookies 来源改为 Chrome/Edge，或重新导出 cookies.txt 后重试。");
                }
                else if (line.IndexOf("Failed to decrypt with DPAPI", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cookieErrorHintLogged = true;
                    AppendLog("提示：浏览器 Cookies 解密失败。可关闭浏览器后重试，或改回“Cookies 文件”并重新导出 cookies.txt。");
                }
            }

            var match = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
            if (match.Success)
            {
                double value;
                if (double.TryParse(match.Groups[1].Value, out value))
                {
                    Ui(delegate
                    {
                        ProgressBar.Value = Math.Min(100, Math.Max(0, value));
                        var percentText = value.ToString("0.#") + "%";
                        ProgressTextBlock.Text = currentPlaylistTotal > 0
                            ? "第 " + currentPlaylistIndex + "/" + currentPlaylistTotal + " 个 · " + percentText
                            : percentText;
                    });
                }
            }

            var itemMatch = Regex.Match(line, @"[Dd]ownloading item (\d+) of (\d+)");
            if (itemMatch.Success)
            {
                int itemIndex;
                int itemTotal;
                if (int.TryParse(itemMatch.Groups[1].Value, out itemIndex) &&
                    int.TryParse(itemMatch.Groups[2].Value, out itemTotal))
                {
                    currentPlaylistIndex = itemIndex;
                    currentPlaylistTotal = itemTotal;
                    Ui(delegate
                    {
                        StatusText.Text = "第 " + currentPlaylistIndex + "/" + currentPlaylistTotal + " 个";
                    });
                }
            }

            var progressLine = Regex.Match(line, @"^\s*\[download\]\s+(\d+(?:\.\d+)?)%");
            if (progressLine.Success)
            {
                double value;
                if (double.TryParse(progressLine.Groups[1].Value, out value) &&
                    (value >= lastLoggedPercent + 1 || line.IndexOf(" in ", StringComparison.Ordinal) >= 0))
                {
                    lastLoggedPercent = value;
                    AppendLogSafe(line);
                }
            }
            else
            {
                AppendLogSafe(line);
            }

            var trimmed = line.Trim();
            var isFinalMarker = trimmed.IndexOf("[Merger]", StringComparison.Ordinal) >= 0 ||
                                trimmed.IndexOf("[VideoRemuxer]", StringComparison.Ordinal) >= 0 ||
                                trimmed.IndexOf("[ExtractAudio]", StringComparison.Ordinal) >= 0 ||
                                trimmed.IndexOf("[Metadata]", StringComparison.Ordinal) >= 0 ||
                                trimmed.IndexOf("[EmbedThumbnail]", StringComparison.Ordinal) >= 0;
            if (isFinalMarker)
            {
                finalOutputSeen = true;
            }

            var destination = DownloaderService.ExtractDestinationPath(line);
            if (!string.IsNullOrWhiteSpace(destination))
            {
                lastDownloadDestination = destination;
                if (isFinalMarker)
                {
                    finalPathCandidate = destination;
                }

                try
                {
                    if (File.Exists(destination))
                    {
                        lastCompletedPath = destination;
                    }
                }
                catch
                {
                    // Ignore inaccessible path.
                }
            }

            if (LooksLikeFinalPath(trimmed))
            {
                lastCompletedPath = trimmed;
            }
        }

        private static bool LooksLikeFinalPath(string line)
        {
            if (line.Length <= 4 || line[1] != ':' || line[2] != '\\')
            {
                return false;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) || line.IndexOf("Destination:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            try
            {
                return File.Exists(line);
            }
            catch
            {
                return false;
            }
        }

        private void VerifyAudioStream()
        {
            var filePath = ResolveFinalPath();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AppendLog("未找到最终媒体文件，已跳过音频校验。");
                SetStatus("下载完成（未找到文件）");
                return;
            }

            AppendLog("最终文件：" + filePath);
            var count = DownloaderService.CountAudioStreams(FfprobeTextBox.Text.Trim(), filePath);
            if (count < 0)
            {
                AppendLog("无法调用 ffprobe 校验音频流，请确认 ffprobe 路径正确。");
                SetStatus("下载完成（未校验音频）");
                return;
            }

            if (count == 0)
            {
                AppendLog("警告：结果文件未检测到音频流，请检查下载格式和 ffmpeg 路径。");
                SetStatus("下载完成（未检测到音频）");
                return;
            }

            AppendLog("已检测到音频流数量：" + count);
            SetStatus("下载完成（音频正常）");
        }

        private string ResolveFinalPath()
        {
            if (!string.IsNullOrWhiteSpace(lastCompletedPath) && File.Exists(lastCompletedPath))
            {
                return lastCompletedPath;
            }

            if (!string.IsNullOrWhiteSpace(finalPathCandidate) && File.Exists(finalPathCandidate))
            {
                return finalPathCandidate;
            }

            if (!string.IsNullOrWhiteSpace(lastDownloadDestination) && File.Exists(lastDownloadDestination))
            {
                return lastDownloadDestination;
            }

            if (!string.IsNullOrWhiteSpace(activeOutputFolder) && Directory.Exists(activeOutputFolder))
            {
                return FindNewestMediaFile(activeOutputFolder, downloadStartedAt);
            }

            return null;
        }

        private static string FindNewestMediaFile(string folder, DateTime startedAt)
        {
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp4", ".mkv", ".webm", ".mov", ".avi", ".ts",
                ".flac", ".wav", ".m4a", ".m4b", ".mp3", ".ogg", ".opus"
            };

            FileInfo newest = null;
            try
            {
                foreach (var filePath in Directory.GetFiles(folder))
                {
                    var extension = Path.GetExtension(filePath);
                    if (!allowedExtensions.Contains(extension))
                    {
                        continue;
                    }

                    var info = new FileInfo(filePath);
                    if (info.LastWriteTime < startedAt.AddSeconds(-10))
                    {
                        continue;
                    }

                    if (newest == null || info.LastWriteTime > newest.LastWriteTime)
                    {
                        newest = info;
                    }
                }
            }
            catch
            {
                return null;
            }

            return newest == null ? null : newest.FullName;
        }

        private void PauseCurrent_Click(object sender, RoutedEventArgs e)
        {
            if (currentProcess == null || isPaused || currentProcess.HasExited)
            {
                return;
            }

            if (ProcessControl.Suspend(currentJobHandle, currentProcess))
            {
                isPaused = true;
                AppendLog("已暂停下载。");
                SetBusy(true, "已暂停");
            }
            else
            {
                AppendLog("暂停下载失败，请重试。");
            }
        }

        private void ResumeCurrent_Click(object sender, RoutedEventArgs e)
        {
            if (currentProcess == null || !isPaused || currentProcess.HasExited)
            {
                return;
            }

            if (ProcessControl.Resume(currentJobHandle, currentProcess))
            {
                isPaused = false;
                AppendLog("已继续下载。");
                SetBusy(true, "继续下载中...");
            }
            else
            {
                AppendLog("继续下载失败，请重试。");
            }
        }

        private void StopCurrent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                stopRequested = true;
                if (currentJobHandle != IntPtr.Zero)
                {
                    ProcessControl.Terminate(currentJobHandle);
                }

                if (currentProcess != null && !currentProcess.HasExited)
                {
                    currentProcess.Kill();
                }

                isPaused = false;
                AppendLog("已请求停止下载。");
                SetStatus("已停止");
            }
            catch (Exception ex)
            {
                stopRequested = false;
                AppendLog("停止下载失败：" + ex.Message);
            }
        }

        private void SetBusy(bool busy, string status)
        {
            Ui(delegate
            {
                VideoDownloadButton.IsEnabled = !busy;
                MusicDownloadButton.IsEnabled = !busy;
                VideoPauseButton.IsEnabled = busy && !isPaused;
                VideoResumeButton.IsEnabled = busy && isPaused;
                MusicPauseButton.IsEnabled = busy && !isPaused;
                MusicResumeButton.IsEnabled = busy && isPaused;
                VideoStopButton.IsEnabled = busy;
                MusicStopButton.IsEnabled = busy;

                if (!busy)
                {
                    ProgressBar.Value = 0;
                    ProgressTextBlock.Text = "0%";
                }

                StatusText.Text = status;
            });
        }

        private void SetStatus(string text)
        {
            Ui(delegate { StatusText.Text = text; });
        }

        private void AppendLog(string text)
        {
            Ui(delegate
            {
                LogTextBox.AppendText(text + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });
        }

        private void AppendLogSafe(string text)
        {
            Ui(delegate
            {
                try
                {
                    LogTextBox.AppendText(text + Environment.NewLine);
                    LogTextBox.ScrollToEnd();
                }
                catch
                {
                    // The window may already be closing.
                }
            });
        }

        private void Ui(Action action)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(action);
                return;
            }

            action();
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            var text = LogTextBox.Text;
            Exception lastError = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    AppendLog("日志已复制到剪贴板。");
                    SetStatus("日志已复制");
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    System.Threading.Thread.Sleep(150);
                }
            }

            try
            {
                var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoDownloader-日志.txt");
                if (lastFallbackLogText == text)
                {
                    AppendLog("剪贴板仍被占用，日志内容未变化：" + logFile);
                    SetStatus("日志已存在");
                    return;
                }

                File.WriteAllText(logFile, text, System.Text.Encoding.UTF8);
                lastFallbackLogText = text;
                AppendLog("剪贴板被占用，日志已保存到：" + logFile);
                SetStatus("日志已保存");
                try
                {
                    Process.Start(logFile);
                }
                catch
                {
                    // The file is saved even if no default text editor is available.
                }
            }
            catch (Exception ex)
            {
                AppendLog("复制日志失败：" + (lastError == null ? ex.Message : lastError.Message));
                SetStatus("复制日志失败");
            }
        }

        private void OpenVideoFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(VideoOutputTextBox.Text, "视频保存目录");
        }

        private void OpenMusicFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(MusicOutputTextBox.Text, "音乐保存目录");
        }

        private void OpenFolder(string folder, string label)
        {
            if (Directory.Exists(folder))
            {
                Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            else
            {
                System.Windows.MessageBox.Show(this, label + "不存在，请先创建或重新选择。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void VerifyPaths_Click(object sender, RoutedEventArgs e)
        {
            var missing = new List<string>();
            CheckPath(YtDlpTextBox, "yt-dlp.exe", missing);
            CheckPath(FfmpegTextBox, "ffmpeg.exe", missing);
            CheckPath(FfprobeTextBox, "ffprobe.exe", missing);

            if (missing.Count == 0)
            {
                System.Windows.MessageBox.Show(this, "三个工具路径均可用。", "检查结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(this, "以下路径不存在：\n\n" + string.Join(Environment.NewLine, missing), "检查结果", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void AutoFindTools_Click(object sender, RoutedEventArgs e)
        {
            var roots = BuildSearchRoots();
            var ytDlp = FindLatestTool("yt-dlp.exe", roots);
            var ffmpeg = FindLatestTool("ffmpeg.exe", roots);
            var ffprobe = FindLatestTool("ffprobe.exe", roots);

            var found = new List<string>();
            if (ytDlp != null)
            {
                YtDlpTextBox.Text = ytDlp;
                found.Add("yt-dlp");
            }

            if (ffmpeg != null)
            {
                FfmpegTextBox.Text = ffmpeg;
                found.Add("ffmpeg");
            }

            if (ffprobe != null)
            {
                FfprobeTextBox.Text = ffprobe;
                found.Add("ffprobe");
            }

            SaveSettingsFromUi(false);

            var missing = new List<string>();
            if (ytDlp == null) missing.Add("yt-dlp.exe");
            if (ffmpeg == null) missing.Add("ffmpeg.exe");
            if (ffprobe == null) missing.Add("ffprobe.exe");

            if (missing.Count == 0)
            {
                System.Windows.MessageBox.Show(this, "已找到并保存全部工具路径。", "自动查找", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(this, "已找到：\n" + string.Join("、", found) + "\n\n未找到：\n" + string.Join("、", missing), "自动查找", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ResetPaths_Click(object sender, RoutedEventArgs e)
        {
            YtDlpTextBox.Text = AppSettings.DefaultYtDlp;
            FfmpegTextBox.Text = AppSettings.DefaultFfmpeg;
            FfprobeTextBox.Text = AppSettings.DefaultFfprobe;
            SaveSettingsFromUi(true);
        }

        private static List<string> BuildSearchRoots()
        {
            var roots = new List<string>();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            roots.Add(baseDir);

            var parent = SafeParent(baseDir);
            if (parent != null) roots.Add(parent);

            var grandParent = SafeParent(parent);
            if (grandParent != null) roots.Add(grandParent);
            return roots;
        }

        private static string SafeParent(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                var dir = Directory.GetParent(path);
                return dir == null ? null : dir.FullName;
            }
            catch
            {
                return null;
            }
        }

        private static void AddIfDirectoryExists(List<string> roots, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && !roots.Contains(path))
            {
                roots.Add(path);
            }
        }

        private static string FindLatestTool(string fileName, IEnumerable<string> roots)
        {
            var results = new List<FileInfo>();
            foreach (var root in roots)
            {
                SearchDirectory(root, fileName, 0, results);
            }

            FileInfo latest = null;
            foreach (var file in results)
            {
                if (latest == null || file.LastWriteTime > latest.LastWriteTime)
                {
                    latest = file;
                }
            }

            return latest == null ? null : latest.FullName;
        }

        private static void SearchDirectory(string path, string fileName, int depth, List<FileInfo> results)
        {
            if (depth > 4 || string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                foreach (var file in Directory.GetFiles(path, fileName, SearchOption.TopDirectoryOnly))
                {
                    results.Add(new FileInfo(file));
                }

                foreach (var dir in Directory.GetDirectories(path))
                {
                    if (IsIgnoredDirectory(dir))
                    {
                        continue;
                    }

                    SearchDirectory(dir, fileName, depth + 1, results);
                }
            }
            catch
            {
                // Ignore inaccessible directories.
            }
        }

        private static bool IsIgnoredDirectory(string path)
        {
            var name = Path.GetFileName(path).ToLowerInvariant();
            return name == "bin" || name == "obj" || name == ".git" || name == ".svn" || name == "windows" || name == "program files" || name == "program files (x86)";
        }

        private void OpenHelpDoc_Click(object sender, RoutedEventArgs e)
        {
            var doc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "使用教程.html");
            if (File.Exists(doc))
            {
                Process.Start(doc);
            }
            else
            {
                System.Windows.MessageBox.Show(this, "未找到离线教程文件：\n" + doc, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HelpLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try
            {
                Process.Start(e.Uri.ToString());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, "无法打开链接：\n" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowMissingTool(string toolName, string path)
        {
            System.Windows.MessageBox.Show(
                this,
                toolName + " 不存在，请在“设置”页选择正确的文件。\n\n当前路径：\n" + path,
                "找不到工具",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    internal static class ProcessControl
    {
        private const int JobObjectExtendedLimitInformationClass = 9;
        private const int JobObjectBasicProcessIdList = 3;
        private const uint JobObjectLimitKillOnJobClose = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr job,
            int jobObjectInformationClass,
            IntPtr jobObjectInformation,
            uint jobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryInformationJobObject(
            IntPtr job,
            int jobObjectInformationClass,
            IntPtr jobObjectInformation,
            uint jobObjectInformationLength,
            out uint returnLength);

        public static IntPtr CreateJob()
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
            {
                return job;
            }

            var info = new JobObjectExtendedLimitInformation();
            info.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
            var size = Marshal.SizeOf(typeof(JobObjectExtendedLimitInformation));
            var pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, pointer, false);
                if (!SetInformationJobObject(job, JobObjectExtendedLimitInformationClass, pointer, (uint)size))
                {
                    CloseHandle(job);
                    return IntPtr.Zero;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }

            return job;
        }

        public static bool Assign(IntPtr job, Process process)
        {
            if (job == IntPtr.Zero || process == null || process.HasExited)
            {
                return false;
            }

            try
            {
                return AssignProcessToJobObject(job, process.Handle);
            }
            catch
            {
                // The process may have exited before assignment.
                return false;
            }
        }

        public static bool Suspend(IntPtr job, Process process)
        {
            if (job != IntPtr.Zero)
            {
                if (SuspendJob(job))
                {
                    return true;
                }
            }

            return SuspendProcessFallback(process);
        }

        public static bool Resume(IntPtr job, Process process)
        {
            if (job != IntPtr.Zero)
            {
                if (ResumeJob(job))
                {
                    return true;
                }
            }

            return ResumeProcessFallback(process);
        }

        public static void Terminate(IntPtr job)
        {
            if (job != IntPtr.Zero)
            {
                TerminateJobObject(job, 1);
            }
        }

        public static void Close(IntPtr job)
        {
            if (job != IntPtr.Zero)
            {
                CloseHandle(job);
            }
        }

        private const uint ProcessSuspendResume = 0x0800;

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        private static bool SuspendJob(IntPtr job)
        {
            var processIds = GetJobProcessIds(job);
            if (processIds == null || processIds.Count == 0)
            {
                return false;
            }

            var allSucceeded = true;
            foreach (var processId in processIds)
            {
                var handle = OpenProcess(ProcessSuspendResume, false, processId);
                if (handle == IntPtr.Zero)
                {
                    allSucceeded = false;
                    continue;
                }

                try
                {
                    if (NtSuspendProcess(handle) != 0)
                    {
                        allSucceeded = false;
                    }
                }
                finally
                {
                    CloseHandle(handle);
                }
            }

            return allSucceeded;
        }

        private static bool ResumeJob(IntPtr job)
        {
            var processIds = GetJobProcessIds(job);
            if (processIds == null || processIds.Count == 0)
            {
                return false;
            }

            var allSucceeded = true;
            foreach (var processId in processIds)
            {
                var handle = OpenProcess(ProcessSuspendResume, false, processId);
                if (handle == IntPtr.Zero)
                {
                    allSucceeded = false;
                    continue;
                }

                try
                {
                    if (NtResumeProcess(handle) != 0)
                    {
                        allSucceeded = false;
                    }
                }
                finally
                {
                    CloseHandle(handle);
                }
            }

            return allSucceeded;
        }

        private static List<int> GetJobProcessIds(IntPtr job)
        {
            uint returnLength;
            var bufferSize = 4096;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (!QueryInformationJobObject(job, JobObjectBasicProcessIdList, buffer, (uint)bufferSize, out returnLength))
                {
                    return null;
                }

                var count = Marshal.ReadInt32(buffer, 0);
                if (count <= 0 || count > 512)
                {
                    return null;
                }

                var processIds = new List<int>(count);
                for (var i = 0; i < count; i++)
                {
                    var pid = Marshal.ReadIntPtr(buffer, 8 + (i * IntPtr.Size));
                    processIds.Add(pid.ToInt32());
                }

                return processIds;
            }
            catch
            {
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool SuspendProcessFallback(Process process)
        {
            if (process == null || process.HasExited)
            {
                return false;
            }

            var handle = OpenProcess(ProcessSuspendResume, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return NtSuspendProcess(handle) == 0;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        private static bool ResumeProcessFallback(Process process)
        {
            if (process == null || process.HasExited)
            {
                return false;
            }

            var handle = OpenProcess(ProcessSuspendResume, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return NtResumeProcess(handle) == 0;
            }
            finally
            {
                CloseHandle(handle);
            }
        }
    }
}
