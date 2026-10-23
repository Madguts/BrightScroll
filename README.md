# BrightScroll

BrightScroll is a lightweight Windows tray app that shows one numeric brightness icon for each connected display. Hover over an icon and scroll the mouse wheel to change that display's brightness. Click an icon to open settings for the scroll increment, icon font, text color, and background color.

## Windows behavior and limitations

- BrightScroll registers a separate notification-area icon per Windows display. Since the notification area does not reliably forward wheel events to icons, the app checks whether the cursor is over each icon using `Shell_NotifyIconGetRect` and a low-level mouse-wheel hook.
- Windows may place notification-area icons in its overflow area; BrightScroll cannot force icons to remain visible or create an icon inside each monitor's taskbar.
- Brightness uses the Windows physical-monitor DDC/CI API first, then WMI monitor-brightness methods. Support depends on the display, graphics driver, dock, and monitor settings. Enable DDC/CI in an external monitor's on-screen menu where available. Displays without a usable API show `--`; Settings shows the device status.
- Percentages are clamped to 0–100. Some displays expose a smaller native range, which is mapped to those percentages.

## Build and install

Requirements: Windows 10 or 11, .NET 8 SDK, and Inno Setup 6 for the installer.

1. Run `dotnet run --project BrightScroll.Core.Tests/BrightScroll.Core.Tests.csproj` for portable brightness and settings checks.
2. Run `scripts\publish-windows.ps1` in PowerShell. This publishes a self-contained, single-file `win-x64` app.
3. Compile `installer\BrightScroll.iss` using Inno Setup 6 to produce `artifacts\BrightScroll-Setup.exe`.
4. Run the installer. Starting BrightScroll when you sign in is an optional installer task.

Settings are saved in `%APPDATA%\BrightScroll\settings.json`. BrightScroll runs without administrator privileges.
