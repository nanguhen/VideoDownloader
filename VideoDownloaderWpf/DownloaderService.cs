// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VideoDownloader
{
    internal static class DownloaderService
    {
        public static string GetFfmpegDirectory(string ffmpegPath, string ffprobePath)
        {
            if (!string.IsNullOrWhiteSpace(ffmpegPath) && File.Exists(ffmpegPath))
            {
                return Path.GetDirectoryName(ffmpegPath);
            }

            if (!string.IsNullOrWhiteSpace(ffprobePath) && File.Exists(ffprobePath))
            {
                return Path.GetDirectoryName(ffprobePath);
            }

            return string.Empty;
        }

        public static string BuildListFormatsArguments(string url, string ffmpegDir, string cookiesPath, string cookiesSource, string proxy, string impersonateClient)
        {
            var args = new List<string>
            {
                "--encoding",
                "utf-8",
                "--ffmpeg-location",
                ffmpegDir,
                "--no-playlist"
            };

            AddNetworkArguments(args, cookiesPath, cookiesSource, proxy);
            AddImpersonateArguments(args, impersonateClient, url);
            args.Add("-F");
            args.Add(url);

            return Join(args);
        }

        public static string BuildVideoDownloadArguments(
            string url,
            string outputFolder,
            int formatMode,
            bool overwrite,
            string cookiesPath,
            string cookiesSource,
            string proxy,
            string ffmpegDir,
            string impersonateClient)
        {
            var args = new List<string>
            {
                "--encoding",
                "utf-8",
                "--ffmpeg-location",
                ffmpegDir,
                "--newline",
                "--no-playlist"
            };

            if (overwrite)
            {
                args.Add("--force-overwrites");
            }

            AddSpeedArguments(args, url);
            AddNetworkArguments(args, cookiesPath, cookiesSource, proxy);
            AddImpersonateArguments(args, impersonateClient, url);

            if (formatMode == 0)
            {
                args.Add("-t");
                args.Add("mp4");
            }
            else if (formatMode == 2)
            {
                args.Add("-x");
                args.Add("--audio-format");
                args.Add("mp3");
                args.Add("--audio-quality");
                args.Add("0");
            }
            else
            {
                args.Add("-f");
                args.Add("bestvideo*+bestaudio/best");
            }

            args.Add("-o");
            args.Add(Path.Combine(outputFolder, "%(title)s [%(id)s].%(ext)s"));
            args.Add(url);

            return Join(args);
        }

        public static string BuildMusicDownloadArguments(
            string url,
            string outputFolder,
            int musicFormat,
            bool embedMetadata,
            bool overwrite,
            string cookiesPath,
            string cookiesSource,
            string proxy,
            string ffmpegDir,
            string impersonateClient,
            bool playlistMode,
            string playlistRange,
            bool playlistSubfolder)
        {
            var args = new List<string>
            {
                "--encoding",
                "utf-8",
                "--ffmpeg-location",
                ffmpegDir,
                "--newline"
            };

            args.Add(playlistMode ? "--yes-playlist" : "--no-playlist");

            if (playlistMode)
            {
                if (!string.IsNullOrWhiteSpace(playlistRange))
                {
                    args.Add("--playlist-items");
                    args.Add(playlistRange.Trim());
                }

                args.Add("--ignore-errors");
                args.Add("--download-archive");
                args.Add(Path.Combine(outputFolder, "VideoDownloader-music-archive.txt"));
            }

            if (overwrite)
            {
                args.Add("--force-overwrites");
            }

            AddSpeedArguments(args, url);
            AddNetworkArguments(args, cookiesPath, cookiesSource, proxy);
            AddImpersonateArguments(args, impersonateClient, url);
            args.Add("-x");
            args.Add("--audio-format");
            args.Add(GetMusicFormatName(musicFormat));
            args.Add("--audio-quality");
            args.Add("0");

            if (embedMetadata)
            {
                args.Add("--embed-metadata");
                if (musicFormat != 2)
                {
                    args.Add("--embed-thumbnail");
                }
            }

            args.Add("-o");
            if (playlistMode && playlistSubfolder)
            {
                args.Add(Path.Combine(outputFolder, "%(playlist_title)s", "%(playlist_index)02d - %(title)s [%(id)s].%(ext)s"));
            }
            else if (playlistMode)
            {
                args.Add(Path.Combine(outputFolder, "%(playlist_index)02d - %(title)s [%(id)s].%(ext)s"));
            }
            else
            {
                args.Add(Path.Combine(outputFolder, "%(title)s [%(id)s].%(ext)s"));
            }

            args.Add(url);

            return Join(args);
        }

        public static string BuildThumbnailArguments(string url, string cookiesPath, string cookiesSource, string proxy, string impersonateClient)
        {
            var args = new List<string>
            {
                "--encoding",
                "utf-8",
                "--no-playlist",
                "--get-thumbnail"
            };

            AddNetworkArguments(args, cookiesPath, cookiesSource, proxy);
            AddImpersonateArguments(args, impersonateClient, url);
            args.Add(url);
            return Join(args);
        }

        public static string GetMusicFormatName(int format)
        {
            if (format == 1)
            {
                return "alac";
            }

            if (format == 2)
            {
                return "wav";
            }

            return "flac";
        }

        public static int CountAudioStreams(string ffprobePath, string filePath)
        {
            if (string.IsNullOrWhiteSpace(ffprobePath) || !File.Exists(ffprobePath) ||
                string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return -1;
            }

            var arguments = "-v error -show_entries stream=codec_type -of csv=p=0 \"" + filePath + "\"";
            var output = RunProcessCapture(ffprobePath, arguments);
            if (string.IsNullOrWhiteSpace(output))
            {
                return 0;
            }

            var count = 0;
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Trim().Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        public static string RunProcessCapture(string fileName, string arguments)
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

        private static void AddNetworkArguments(List<string> args, string cookiesPath, string cookiesSource, string proxy)
        {
            var source = string.IsNullOrWhiteSpace(cookiesSource) ? "file" : cookiesSource.Trim().ToLowerInvariant();
            if (source == "chrome" || source == "edge" || source == "firefox")
            {
                args.Add("--cookies-from-browser");
                args.Add(source);
            }
            else if (source == "file" && !string.IsNullOrWhiteSpace(cookiesPath) && File.Exists(cookiesPath))
            {
                args.Add("--cookies");
                args.Add(cookiesPath);
            }

            if (!string.IsNullOrWhiteSpace(proxy))
            {
                args.Add("--proxy");
                args.Add(proxy);
            }
        }

        public static string ResolveImpersonateTarget(string impersonateClient, string url)
        {
            var mode = string.IsNullOrWhiteSpace(impersonateClient) ? "auto" : impersonateClient.Trim();
            if (mode.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (mode.Equals("safari", StringComparison.OrdinalIgnoreCase))
            {
                return "Safari-18.0";
            }

            if (mode.Equals("chrome", StringComparison.OrdinalIgnoreCase))
            {
                return "Chrome-145";
            }

            if (mode.Equals("edge", StringComparison.OrdinalIgnoreCase))
            {
                return "Edge-101";
            }

            if (mode.Equals("firefox", StringComparison.OrdinalIgnoreCase))
            {
                return "Firefox-147";
            }

            if (!mode.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return mode;
            }

            var host = string.Empty;
            try
            {
                host = new Uri(url).Host.ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }

            if (host.EndsWith("bilibili.com", StringComparison.Ordinal))
            {
                return "Safari-18.0";
            }

            if (host.EndsWith("youtube.com", StringComparison.Ordinal) || host.EndsWith("youtu.be", StringComparison.Ordinal))
            {
                return "Chrome-145";
            }

            return string.Empty;
        }

        public static string ExtractDestinationPath(string line)
        {
            var trimmed = line == null ? string.Empty : line.Trim();
            var marker = "[download] Destination: ";
            var audioMarker = "[ExtractAudio] Destination: ";
            var mergerMarker = "[Merger] Merging formats into ";
            var remuxMarker = "[VideoRemuxer] Remuxing video into ";
            var notRemuxMarker = "[VideoRemuxer] Not remuxing media file ";
            var metadataMarker = "[Metadata] Adding metadata to ";
            var thumbnailMarker = "[EmbedThumbnail] mutagen: Adding thumbnail to ";

            string candidate = null;
            if (trimmed.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                candidate = trimmed.Substring(marker.Length);
            }
            else if (trimmed.StartsWith(audioMarker, StringComparison.OrdinalIgnoreCase))
            {
                candidate = trimmed.Substring(audioMarker.Length);
            }
            else if (trimmed.StartsWith(mergerMarker, StringComparison.OrdinalIgnoreCase))
            {
                candidate = trimmed.Substring(mergerMarker.Length);
            }
            else if (trimmed.StartsWith(remuxMarker, StringComparison.OrdinalIgnoreCase))
            {
                candidate = trimmed.Substring(remuxMarker.Length);
            }
            else if (trimmed.StartsWith(notRemuxMarker, StringComparison.OrdinalIgnoreCase))
            {
                candidate = trimmed.Substring(notRemuxMarker.Length);
            }
            else if (trimmed.StartsWith(metadataMarker, StringComparison.OrdinalIgnoreCase))
            {
                candidate = trimmed.Substring(metadataMarker.Length);
            }
            else if (trimmed.StartsWith(thumbnailMarker, StringComparison.OrdinalIgnoreCase))
            {
                candidate = trimmed.Substring(thumbnailMarker.Length);
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            candidate = candidate.Trim();
            if (candidate.Length > 1 && candidate[0] == '"' && candidate[candidate.Length - 1] == '"')
            {
                candidate = candidate.Substring(1, candidate.Length - 2);
            }

            return candidate;
        }

        private static void AddImpersonateArguments(List<string> args, string impersonateClient, string url)
        {
            var target = ResolveImpersonateTarget(impersonateClient, url);
            if (!string.IsNullOrWhiteSpace(target))
            {
                args.Add("--impersonate");
                args.Add(target);
            }
        }

        private static void AddSpeedArguments(List<string> args, string url)
        {
            if (!IsYouTubeUrl(url))
            {
                return;
            }

            args.Add("--concurrent-fragments");
            args.Add("8");
            args.Add("--throttled-rate");
            args.Add("100K");
            args.Add("--fragment-retries");
            args.Add("10");
        }

        private static bool IsYouTubeUrl(string url)
        {
            try
            {
                var host = new Uri(url).Host.ToLowerInvariant();
                return host.EndsWith("youtube.com", StringComparison.Ordinal) ||
                       host.EndsWith("youtu.be", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static string Join(List<string> args)
        {
            return string.Join(" ", QuoteAll(args));
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
    }
}
