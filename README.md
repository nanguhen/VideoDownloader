# VideoDownloader

![VideoDownloader icon](VideoDownloaderWpf/Assets/app-icon-256.png)

VideoDownloader 是一个基于 `yt-dlp`、FFmpeg 和 .NET Framework WPF 的 Windows
图形化音视频下载工具。它把常用的视频下载、无损音乐转换、播放列表/分P合集、
Cookies、代理、进度控制和日志排查集成到一个桌面界面中。

## 功能

- 视频下载：最佳 MP4、最佳质量、仅提取 MP3。
- 无损音乐：FLAC、ALAC、WAV 输出。
- 音乐合集：YouTube 播放列表和 B 站分P合集。
- 合集范围：留空下载全部，支持 `1-50`、`1,3,5` 等写法。
- 合集续传：使用下载存档文件跳过已下载条目。
- 暂停、继续、停止：按进程树控制 yt-dlp 及其子进程。
- 进度显示：进度条、数字百分比、合集条目序号。
- Cookies：支持 Netscape `cookies.txt`，也支持 Chrome、Edge、Firefox。
- 浏览器伪装：支持自动、Safari、Chrome、Edge、Firefox。
- 代理：支持 HTTP/HTTPS/SOCKS 代理地址。
- 日志：保留完整 yt-dlp 输出，支持复制，剪贴板不可用时保存为文件。
- 主题：极简浅色、雾蓝、青瓷三套皮肤。
- 工具路径可配置，升级 yt-dlp 或 FFmpeg 后不需要重新编译程序。

## 运行要求

- Windows 10 或 Windows 11。
- .NET Framework 4.8。
- `yt-dlp.exe`
- `ffmpeg.exe`
- `ffprobe.exe`

程序不会内置或自动下载上述三个第三方工具。

## 下载与使用

优先从 GitHub Releases 下载 `VideoDownloader-v1.0.0-portable.zip`，解压后：

1. 将 `yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe` 放在任意目录。
2. 启动 `VideoDownloader.exe`。
3. 打开“设置”，选择三个工具的正确路径。
4. 点击“测试工具路径”确认可用。
5. 保存设置后开始下载。

配置文件 `VideoDownloader.config` 会自动生成在 `VideoDownloader.exe`
同目录，不写注册表。整个程序目录可以复制到其他位置使用。

## 合集下载

### YouTube 播放列表

1. 打开 YouTube 播放列表页面并复制地址。
2. 在“无损音乐”标签粘贴地址。
3. 勾选“合集模式”。
4. 合集范围留空表示全部，也可以填写 `1-50` 或 `1,3,5`。
5. 点击“开始下载”。

### B 站分P

1. 打开包含多个分P的 B 站视频页面。
2. 复制带 `BV` 的地址。
3. 在“无损音乐”标签粘贴地址。
4. 勾选“合集模式”。
5. 按需填写合集范围。

合集文件会按“合集标题/序号 - 标题 [ID].扩展名”保存到输出目录。

## Cookies 与登录验证

YouTube 可能要求登录验证。设置页提供以下 Cookies 来源：

- `Cookies 文件`：选择 Netscape 格式的 `cookies.txt`。
- `Chrome`：使用 `--cookies-from-browser chrome`。
- `Edge`：使用 `--cookies-from-browser edge`。
- `Firefox`：使用 `--cookies-from-browser firefox`。
- `不使用`：不读取 Cookies。

如果浏览器 Cookies 出现 DPAPI 解密失败，请关闭浏览器后重试，或重新导出
`cookies.txt` 后改回“Cookies 文件”。

## 编译

主程序使用旧式 MSBuild 和 .NET Framework 4.8，不需要 NuGet 包：

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' `
  '.\VideoDownloaderWpf\VideoDownloaderWpf.csproj' `
  /t:Rebuild `
  /p:Configuration=Release
```

输出文件：

```text
VideoDownloaderWpf\bin\Release\VideoDownloader.exe
```

## 项目结构

```text
VideoDownloaderWpf/     WPF 主程序源码
yt-dlp-wrapper/         Playnite Extra Metadata Loader 使用的 yt-dlp 包装程序
VideoDownloader/        早期 WinForms 版本源码
tools/                  图标生成工具源码
```

## 常见问题

### 提示找不到工具

在“设置”页重新选择 `yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe`，或点击
“自动查找工具”。

### YouTube 提示 Sign in to confirm you're not a bot

把 Cookies 来源切换为 Chrome 或 Edge，或重新导出有效的 `cookies.txt`。

### 下载的视频没有声音

确认 FFmpeg 路径正确，并使用“最佳 MP4”或“最佳质量”。

### 日志复制失败

剪贴板可能被其他程序占用。程序会自动重试，并将日志保存为
`VideoDownloader-日志.txt`。

## 第三方依赖

- yt-dlp: https://github.com/yt-dlp/yt-dlp
- yt-dlp supported sites: https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md
- FFmpeg: https://ffmpeg.org/download.html
- FFmpeg Windows builds: https://github.com/BtbN/FFmpeg-Builds/releases

详细说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## License

Copyright (C) 2026 VideoDownloader contributors.

This project is licensed under the GNU General Public License v3.0 only
(`GPL-3.0-only`). Any distributed derivative work must remain open source
under the same license.

See [LICENSE](LICENSE) for the full license text.
