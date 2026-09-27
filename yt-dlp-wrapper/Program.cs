// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

internal static class Program
{
    private static int Main(string[] args)
    {
        var forwardedArgs = FixMp4Format(args);

        var wrapperDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
        var realYtDlp = Path.GetFullPath(Path.Combine(wrapperDir, "..", "yt-dlp.exe"));

        if (!File.Exists(realYtDlp))
        {
            Console.Error.WriteLine("yt-dlp wrapper: real executable not found: " + realYtDlp);
            return 2;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = realYtDlp,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            Arguments = BuildArguments(forwardedArgs)
        };

        using (var process = Process.Start(startInfo))
        {
            if (process == null)
            {
                Console.Error.WriteLine("yt-dlp wrapper: failed to start real yt-dlp.");
                return 3;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
    }

    private static string[] FixMp4Format(string[] args)
    {
        var result = (string[])args.Clone();
        for (var i = 0; i < result.Length - 1; i++)
        {
            var option = result[i];
            var value = result[i + 1];
            var isFormatOption = option == "-f" || option == "--format";
            var isPlainMp4 = string.Equals(value, "mp4", StringComparison.OrdinalIgnoreCase);

            if (isFormatOption && isPlainMp4)
            {
                result[i] = "-t";
            }
        }

        return result;
    }

    private static string BuildArguments(string[] args)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Quote(args[i]));
        }

        return builder.ToString();
    }

    private static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
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
