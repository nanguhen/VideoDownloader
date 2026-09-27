// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VideoDownloader
{
    internal sealed class AppSettings
    {
        public static readonly string DefaultYtDlp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
        public static readonly string DefaultFfmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        public static readonly string DefaultFfprobe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");

        public string YtDlpPath { get; set; }
        public string FfmpegPath { get; set; }
        public string FfprobePath { get; set; }
        public string OutputFolder { get; set; }
        public string MusicOutputFolder { get; set; }
        public string CookiesPath { get; set; }
        public string CookiesSource { get; set; }
        public string Proxy { get; set; }
        public int FormatMode { get; set; }
        public int MusicFormat { get; set; }
        public int ThemeMode { get; set; }
        public bool EmbedMetadata { get; set; }
        public string ImpersonateClient { get; set; }
        public bool MusicPlaylistMode { get; set; }
        public string MusicPlaylistRange { get; set; }
        public bool MusicPlaylistSubfolder { get; set; }

        public static string ConfigFile
        {
            get
            {
                var dir = AppDomain.CurrentDomain.BaseDirectory;
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
                MusicOutputFolder = GetDefaultOutputFolder(),
                CookiesPath = string.Empty,
                CookiesSource = "file",
                Proxy = string.Empty,
                FormatMode = 0,
                MusicFormat = 0,
                ThemeMode = 0,
                EmbedMetadata = true,
                ImpersonateClient = "auto",
                MusicPlaylistMode = false,
                MusicPlaylistRange = string.Empty,
                MusicPlaylistSubfolder = true
            };

            try
            {
                if (File.Exists(ConfigFile))
                {
                    foreach (var line in File.ReadAllLines(ConfigFile, Encoding.UTF8))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
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
                            case "MusicOutputFolder":
                                settings.MusicOutputFolder = value;
                                break;
                            case "CookiesPath":
                                settings.CookiesPath = value;
                                break;
                            case "CookiesSource":
                                settings.CookiesSource = value;
                                break;
                            case "Proxy":
                                settings.Proxy = value;
                                break;
                            case "FormatMode":
                                settings.FormatMode = ParseInt(value, settings.FormatMode);
                                break;
                            case "MusicFormat":
                                settings.MusicFormat = ParseInt(value, settings.MusicFormat);
                                break;
                            case "ThemeMode":
                                settings.ThemeMode = ParseInt(value, settings.ThemeMode);
                                break;
                            case "EmbedMetadata":
                                settings.EmbedMetadata = ParseBool(value, settings.EmbedMetadata);
                                break;
                            case "ImpersonateClient":
                                settings.ImpersonateClient = value;
                                break;
                            case "MusicPlaylistMode":
                                settings.MusicPlaylistMode = ParseBool(value, settings.MusicPlaylistMode);
                                break;
                            case "MusicPlaylistRange":
                                settings.MusicPlaylistRange = value;
                                break;
                            case "MusicPlaylistSubfolder":
                                settings.MusicPlaylistSubfolder = ParseBool(value, settings.MusicPlaylistSubfolder);
                                break;
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed settings and fall back to defaults.
            }

            if (string.IsNullOrWhiteSpace(settings.MusicOutputFolder))
            {
                settings.MusicOutputFolder = settings.OutputFolder;
            }

            if (string.IsNullOrWhiteSpace(settings.ImpersonateClient))
            {
                settings.ImpersonateClient = "auto";
            }

            if (string.IsNullOrWhiteSpace(settings.CookiesSource))
            {
                settings.CookiesSource = "file";
            }

            return settings;
        }

        private static int ParseInt(string value, int fallback)
        {
            int result;
            return int.TryParse(value, out result) ? result : fallback;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool result;
            return bool.TryParse(value, out result) ? result : fallback;
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
                "MusicOutputFolder=" + MusicOutputFolder,
                "CookiesPath=" + CookiesPath,
                "CookiesSource=" + CookiesSource,
                "Proxy=" + Proxy,
                "FormatMode=" + FormatMode,
                "MusicFormat=" + MusicFormat,
                "ThemeMode=" + ThemeMode,
                "EmbedMetadata=" + (EmbedMetadata ? "true" : "false"),
                "ImpersonateClient=" + ImpersonateClient,
                "MusicPlaylistMode=" + (MusicPlaylistMode ? "true" : "false"),
                "MusicPlaylistRange=" + MusicPlaylistRange,
                "MusicPlaylistSubfolder=" + (MusicPlaylistSubfolder ? "true" : "false")
            };

            File.WriteAllLines(ConfigFile, lines, Encoding.UTF8);
        }
    }
}
