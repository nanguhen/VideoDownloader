// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Windows;

namespace VideoDownloader
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Array.IndexOf(e.Args, "--check") >= 0)
            {
                var config = AppSettings.Load();
                Console.WriteLine("YtDlpPath=" + config.YtDlpPath);
                Console.WriteLine("FfmpegPath=" + config.FfmpegPath);
                Console.WriteLine("FfprobePath=" + config.FfprobePath);
                Console.WriteLine("OutputFolder=" + config.OutputFolder);
                Console.WriteLine("MusicOutputFolder=" + config.MusicOutputFolder);
                Console.WriteLine("CookiesPath=" + config.CookiesPath);
                Console.WriteLine("CookiesSource=" + config.CookiesSource);
                Console.WriteLine("Proxy=" + config.Proxy);
                Console.WriteLine("FormatMode=" + config.FormatMode);
                Console.WriteLine("MusicFormat=" + config.MusicFormat);
                Console.WriteLine("ThemeMode=" + config.ThemeMode);
                Console.WriteLine("EmbedMetadata=" + config.EmbedMetadata);
                Console.WriteLine("ImpersonateClient=" + config.ImpersonateClient);
                Console.WriteLine("MusicPlaylistMode=" + config.MusicPlaylistMode);
                Console.WriteLine("MusicPlaylistRange=" + config.MusicPlaylistRange);
                Console.WriteLine("MusicPlaylistSubfolder=" + config.MusicPlaylistSubfolder);
                Console.WriteLine("YtDlpExists=" + File.Exists(config.YtDlpPath));
                Console.WriteLine("FfmpegExists=" + File.Exists(config.FfmpegPath));
                Console.WriteLine("FfprobeExists=" + File.Exists(config.FfprobePath));
                Shutdown();
                return;
            }

            var settings = AppSettings.Load();
            ThemeManager.ApplyTheme(settings.ThemeMode);

            var window = new MainWindow(settings);
            window.Show();
        }
    }
}
