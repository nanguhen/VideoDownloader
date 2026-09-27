# Third-Party Notices

VideoDownloader is an independent desktop front end for `yt-dlp` and FFmpeg.
The source repository does not bundle or redistribute those third-party
programs.

## yt-dlp

- Project: https://github.com/yt-dlp/yt-dlp
- License: Unlicense
- Purpose: Website extraction, media downloading, playlist handling, subtitle,
  metadata, and post-processing orchestration.

## FFmpeg

- Project: https://ffmpeg.org/
- Windows builds: https://github.com/BtbN/FFmpeg-Builds/releases
- Purpose: Audio extraction, media conversion, stream merging, metadata and
  thumbnail embedding, and media inspection through `ffprobe`.
- License note: FFmpeg licensing depends on the selected build. Some Windows
  builds enable GPL components. If you redistribute FFmpeg binaries together
  with this application, verify and satisfy the license terms of that exact
  build, including source and notice obligations.

## Windows and .NET Framework

The application targets .NET Framework 4.8 and uses the Windows desktop stack.
It does not include the .NET Framework runtime.

## User Responsibility

Users are responsible for complying with website terms of service, copyright
law, and the licenses of any external tools used with VideoDownloader.
