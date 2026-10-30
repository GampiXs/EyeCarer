# EyeCarer v1.0.0 — First Public Release

Take a break. Give your eyes a rest.

EyeCarer runs in the Windows tray and reminds you to take short screen breaks with
a customizable fullscreen overlay. This is the first public semantic version;
earlier local development labels were not published releases.

## Features

- Adjustable break interval and duration; manual rest, one-hour pause and resume.
- Desktop blur, static monochrome grain, red radial haze and a custom center message.
- Dark Settings interface with Save & Apply, Cancel, defaults and an unsaved five-second preview.
- Esc dismissal, single instance, per-user Startup shortcut and local atomic JSON configuration.

## Installation

Download **EyeCarer-win-x64.zip**, extract the whole archive to a stable writable folder,
and run **EyeCarer.exe**. No Visual Studio, SDK or separate .NET runtime is required.
Windows 11 x64 is verified. This self-contained .NET 8 build is unsigned; Windows may
show an unknown-publisher/reputation notice. SHA256SUMS.txt identifies the exact archive.

The first break occurs after 15 minutes and lasts 15 seconds. Open Settings by
double-clicking the tray icon. Start with Windows is enabled by default; running the
app creates its own user Startup shortcut. Disable it before moving/removing the app.
Choose Exit EyeCarer to quit. For removal, disable autostart, exit, then delete the folder.

## Privacy and limitations

The desktop is captured locally into memory before each break and used as a static
blurred background. The application does not save or transmit captures and has no
analytics or advertising. Settings/backups and optional error/timing logs are local.

Multi-monitor/mixed DPI behavior is implemented but not hardware-verified. Windows 10,
physical sleep/resume, a fresh Windows login and a multi-hour soak test are unverified.
Capture failure uses a dark fallback. WPF temporarily allocates extra graphics memory.

EyeCarer is a reminder utility, not medical software. Its visual effects are not
proven treatments or protection against vision deterioration.

App code and original icon: MIT. Bundled Microsoft runtime licenses/notices are included.
