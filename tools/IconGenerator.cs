// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

internal static class IconGenerator
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };

    private static void Main(string[] args)
    {
        var target = args.Length > 0 ? args[0] : @"E:\codex project\yt-dlp使用\VideoDownloaderWpf\Assets\app.ico";
        var directory = Path.GetDirectoryName(Path.GetFullPath(target));
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var pngs = new List<byte[]>();
        foreach (var size in Sizes)
        {
            using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);
                DrawIcon(graphics, size);
                pngs.Add(EncodePng(bitmap));
            }
        }

        WriteIco(target, pngs);
        Console.WriteLine("Wrote " + target);

        var pngPreview = Path.Combine(directory, "app-icon-256.png");
        File.WriteAllBytes(pngPreview, pngs[pngs.Count - 1]);
        Console.WriteLine("Wrote " + pngPreview);
    }

    private static void DrawIcon(Graphics graphics, int size)
    {
        var rect = new RectangleF(0, 0, size, size);
        using (var path = RoundedRectangle(rect, size * 0.22f))
        using (var brush = new LinearGradientBrush(rect, Color.FromArgb(255, 20, 184, 166), Color.FromArgb(255, 29, 78, 216), 45f))
        {
            graphics.FillPath(brush, path);
        }

        var white = Color.FromArgb(255, 255, 255, 255);
        var soft = Color.FromArgb(185, 255, 255, 255);
        var penWidth = Math.Max(1.3f, size * 0.10f);

        using (var arrowPen = new Pen(white, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            var cx = size * 0.50f;
            var top = size * 0.28f;
            var tip = size * 0.66f;
            graphics.DrawLine(arrowPen, cx, top, cx, size * 0.58f);
            graphics.DrawLine(arrowPen, size * 0.34f, size * 0.49f, cx, tip);
            graphics.DrawLine(arrowPen, cx, tip, size * 0.66f, size * 0.49f);
            graphics.DrawLine(arrowPen, size * 0.31f, size * 0.80f, size * 0.69f, size * 0.80f);
        }

        using (var wavePen = new Pen(soft, Math.Max(1f, size * 0.055f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            graphics.DrawArc(wavePen, size * 0.64f, size * 0.20f, size * 0.22f, size * 0.22f, 180f, 180f);
            graphics.DrawArc(wavePen, size * 0.70f, size * 0.28f, size * 0.10f, size * 0.10f, 180f, 180f);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180f, 90f);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270f, 90f);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    private static byte[] EncodePng(Bitmap bitmap)
    {
        using (var stream = new MemoryStream())
        {
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }

    private static void WriteIco(string path, List<byte[]> pngs)
    {
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)pngs.Count);

            var offset = 6 + (16 * pngs.Count);
            for (var i = 0; i < pngs.Count; i++)
            {
                var size = Sizes[i];
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(pngs[i].Length);
                writer.Write(offset);
                offset += pngs[i].Length;
            }

            foreach (var png in pngs)
            {
                writer.Write(png);
            }
        }
    }
}
