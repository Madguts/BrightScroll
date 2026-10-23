# BrightScroll

BrightScroll is a lightweight Windows tray app that shows one numeric brightness icon for each connected display. The icons have a transparent background and show a large brightness number; hover to see the monitor name, then scroll the mouse wheel over an icon to change that display's brightness. Click an icon to open settings for the scroll increment and each display's font and text color.

## Windows behavior and limitations

- BrightScroll registers a separate notification-area icon per Windows display. Since the notification area does not reliably forward wheel events to icons, the app checks whether the cursor is over each icon using `Shell_NotifyIconGetRect` and a low-level mouse-wheel hook.
- Windows may place notification-area icons in its overflow area; BrightScroll cannot force icons to remain visible or create an icon inside each monitor's taskbar.
- The icon bitmap is rendered with a transparent background. Windows controls notification-area icon sizing and rendering, so final appearance may vary with Windows versions and shell settings.
- Monitor names come from Windows' connected-display metadata. If Windows does not provide a friendly model name, BrightScroll uses the Windows display device name. Font and text color are saved per monitor using its device identity when available, so settings follow the display rather than its current `DISPLAYn` ordering.
- Brightness uses the Windows physical-monitor DDC/CI API first, then WMI monitor-brightness methods. Support depends on the display, graphics driver, dock, and monitor settings. Enable DDC/CI in an external monitor's on-screen menu where available. Displays without a usable API show `--`; Settings shows the device status.
- Percentages are clamped to 0–100. Some displays expose a smaller native range, which is mapped to those percentages.

## Install or update

1. Open the [BrightScroll Releases](https://github.com/Madguts/BrightScroll/releases) page and download `BrightScroll-Setup.exe` from the latest release.
2. Run the installer. Choose whether BrightScroll should start when you sign in.
3. To update, download and run the installer from a newer release. Your settings in `%APPDATA%\BrightScroll\settings.json` are retained.

BrightScroll runs without administrator privileges.

## Build from source

Requirements: Windows 10 or 11, .NET 8 SDK, and Inno Setup 6 for the installer.

1. Run `dotnet run --project BrightScroll.Core.Tests/BrightScroll.Core.Tests.csproj` for portable brightness, settings migration, and persistence checks.
2. Run `scripts\publish-windows.ps1 -Version 1.2.3` in PowerShell to publish a self-contained, single-file `win-x64` app with that version in its metadata.
3. Compile the installer with `& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DAppVersion=1.2.3 installer\BrightScroll.iss`. The installer recipe requires a version supplied this way; it does not contain a fixed release version.
4. Run `artifacts\BrightScroll-Setup.exe`.

GitHub Actions builds the Windows installer when a semantic-version tag such as `v1.2.3` is pushed and attaches it to that tag's release. The tag's version is passed to both the application publish and installer build. To build for an existing release, run the **Windows release** workflow manually and enter its tag; existing assets with the same filename are left untouched.
