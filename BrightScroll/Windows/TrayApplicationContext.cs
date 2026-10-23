using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BrightScroll.Core;

namespace BrightScroll.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int TrayCallbackMessage = NativeMethods.WmApp + 1;
    private const int WheelMessage = NativeMethods.WmApp + 2;
    private const int RefreshMessage = NativeMethods.WmApp + 3;
    private const uint WheelDelta = 120;
    private readonly AppSettings _settings;
    private readonly string _settingsPath;
    private readonly DisplayBrightnessService _brightness = new();
    private readonly Dictionary<string, DisplayEntry> _displays = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, DisplayEntry> _displayByIndex = new();
    private readonly Dictionary<int, int> _wheelRemainders = new();
    private readonly ConcurrentQueue<(int Index, BrightnessStatus Status)> _statusUpdates = new();
    private readonly NativeMethods.HookProcedure _hookCallback;
    private readonly TrayWindow _window;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private SettingsForm? _settingsForm;
    private IntPtr _mouseHook;
    private uint _taskbarCreatedMessage;
    private bool _refreshing;
    private int _nextIndex;

    internal TrayApplicationContext(AppSettings settings, string settingsPath)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _hookCallback = MouseHookCallback;
        _window = new TrayWindow(this);
        _window.CreateHandle(new CreateParams());
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += (_, _) => RefreshDisplays();
        _refreshTimer.Start();
        RefreshDisplays();

        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _hookCallback, IntPtr.Zero, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Could not register mouse-wheel detection (error {Marshal.GetLastWin32Error()}).");
        }
    }

    private static DisplayEntry DescribeDisplay(Screen screen)
    {
        var monitor = new NativeMethods.DisplayDevice
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.DisplayDevice>(),
            DeviceName = string.Empty,
            DeviceString = string.Empty,
            DeviceId = string.Empty,
            DeviceKey = string.Empty
        };
        var hasMonitor = NativeMethods.EnumDisplayDevices(
            screen.DeviceName,
            0,
            ref monitor,
            NativeMethods.EddGetDeviceInterfaceName);
        var displayName = hasMonitor && !string.IsNullOrWhiteSpace(monitor.DeviceString)
            ? monitor.DeviceString.Trim()
            : screen.DeviceName;
        var appearanceIdentity = hasMonitor && !string.IsNullOrWhiteSpace(monitor.DeviceId)
            ? $"device-id:{monitor.DeviceId.Trim()}"
            : hasMonitor && !string.IsNullOrWhiteSpace(monitor.DeviceKey)
                ? $"device-key:{monitor.DeviceKey.Trim()}"
                : $"display:{screen.DeviceName}";
        return new DisplayEntry(screen.DeviceName, displayName, appearanceIdentity, null);
    }

    private IntPtr Handle => _window.Handle;

    private IEnumerable<DisplayEntry> Entries => _displays.Values;

    private void RefreshDisplays()
    {
        var screens = Screen.AllScreens;
        var connected = screens.Select(screen => screen.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var displaysChanged = false;
        foreach (var deviceName in _displays.Keys.Where(name => !connected.Contains(name)).ToArray())
        {
            RemoveIcon(_displays[deviceName]);
            _displayByIndex.Remove(_displays[deviceName].Index);
            _wheelRemainders.Remove(_displays[deviceName].Index);
            _displays.Remove(deviceName);
            displaysChanged = true;
        }

        foreach (var screen in screens)
        {
            var discovered = DescribeDisplay(screen);
            if (_displays.TryGetValue(screen.DeviceName, out var existing))
            {
                var identityChanged = !string.Equals(
                    existing.AppearanceIdentity,
                    discovered.AppearanceIdentity,
                    StringComparison.OrdinalIgnoreCase);
                var nameChanged = !string.Equals(existing.DisplayName, discovered.DisplayName, StringComparison.Ordinal);
                if (identityChanged)
                {
                    RemoveIcon(existing);
                    existing.DisplayName = discovered.DisplayName;
                    existing.AppearanceIdentity = discovered.AppearanceIdentity;
                    existing.Id = NotificationIconIdentity.Create(discovered.AppearanceIdentity);
                    AddIcon(existing);
                    displaysChanged = true;
                }
                else if (nameChanged)
                {
                    existing.DisplayName = discovered.DisplayName;
                    UpdateIcon(existing);
                    displaysChanged = true;
                }

                continue;
            }

            var display = discovered;
            display.Index = ++_nextIndex;
            _displays.Add(screen.DeviceName, display);
            _displayByIndex.Add(display.Index, display);
            AddIcon(display);
            displaysChanged = true;
        }

        if (displaysChanged)
        {
            _settingsForm?.UpdateDisplays(Entries);
        }

        _ = RefreshBrightnessAsync();
    }

    private void AddIcon(DisplayEntry display)
    {
        var oldIcon = display.Icon;
        display.Icon = CreateIcon(display);
        var data = CreateNotifyIconData(display);
        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NimAdd, ref data))
        {
            display.Icon.Dispose();
            display.Icon = oldIcon;
            throw new InvalidOperationException(
                $"Could not create the tray icon for {display.DeviceName} (error {Marshal.GetLastWin32Error()}).");
        }

        oldIcon?.Dispose();
        data.TimeoutOrVersion = 4;
        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NimSetVersion, ref data))
        {
            RemoveIcon(display);
            throw new InvalidOperationException("Could not configure Windows notification-area callbacks.");
        }
    }

    private void UpdateIcon(DisplayEntry display)
    {
        var newIcon = CreateIcon(display);
        var oldIcon = display.Icon;
        display.Icon = newIcon;
        var data = CreateNotifyIconData(display);
        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NimModify, ref data))
        {
            newIcon.Dispose();
            display.Icon = oldIcon;
            throw new InvalidOperationException(
                $"Could not update the tray icon for {display.DeviceName} (error {Marshal.GetLastWin32Error()}).");
        }

        oldIcon?.Dispose();
    }

    private void RemoveIcon(DisplayEntry display)
    {
        var data = CreateNotifyIconData(display);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimDelete, ref data);
        display.Icon?.Dispose();
        display.Icon = null;
    }

    private NativeMethods.NotifyIconData CreateNotifyIconData(DisplayEntry display)
    {
        return new NativeMethods.NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            WindowHandle = Handle,
            Id = 1,
            Flags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip |
                NativeMethods.NifGuid | NativeMethods.NifShowTip,
            CallbackMessage = TrayCallbackMessage,
            IconHandle = display.Icon?.Handle ?? IntPtr.Zero,
            Tip = TruncateTooltip(display.DisplayName),
            Info = string.Empty,
            InfoTitle = string.Empty,
            Guid = display.Id
        };
    }

    private Icon CreateIcon(DisplayEntry display)
    {
        var appearance = _settings.GetAppearance(display.AppearanceIdentity);
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var foreground = new SolidBrush(ColorTranslator.FromHtml(appearance.TextColor)))
        {
            graphics.Clear(Color.Transparent);
            var text = display.Status?.Percent is int percent ? percent.ToString() : "--";
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateIconTextPath(text, appearance.FontFamily, bitmap.Size);
            graphics.FillPath(foreground, path);
        }

        var iconHandle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(iconHandle).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(iconHandle);
        }
    }

    private static string TruncateTooltip(string text)
    {
        const int maxLength = 127;
        if (text.Length <= maxLength)
        {
            return text;
        }

        var length = maxLength;
        if (char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length]))
        {
            length--;
        }

        return text[..length];
    }

    private static GraphicsPath CreateIconTextPath(string text, string family, Size iconSize)
    {
        FontFamily fontFamily;
        try
        {
            fontFamily = new FontFamily(family);
        }
        catch (ArgumentException)
        {
            fontFamily = new FontFamily("Segoe UI");
        }

        using (fontFamily)
        {
            for (var size = 40f; size >= 8f; size--)
            {
                using var font = new Font(fontFamily, size, FontStyle.Bold, GraphicsUnit.Pixel);
                var path = new GraphicsPath();
                path.AddString(
                    text,
                    fontFamily,
                    (int)FontStyle.Bold,
                    size,
                    PointF.Empty,
                    StringFormat.GenericTypographic);
                var bounds = path.GetBounds();
                if (bounds.Width <= iconSize.Width - 2 && bounds.Height <= iconSize.Height - 2)
                {
                    using var transform = new Matrix();
                    transform.Translate(
                        (iconSize.Width - bounds.Width) / 2 - bounds.X,
                        (iconSize.Height - bounds.Height) / 2 - bounds.Y);
                    path.Transform(transform);
                    return path;
                }

                path.Dispose();
            }
        }

        throw new InvalidOperationException($"Could not render tray icon text '{text}'.");
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == NativeMethods.WmMouseWheel)
        {
            var input = Marshal.PtrToStructure<NativeMethods.LowLevelMouseHookData>(lParam);
            var delta = unchecked((short)(input.MouseData >> 16));
            if (delta != 0)
            {
                foreach (var display in Entries)
                {
                    var identifier = new NativeMethods.NotifyIconIdentifier
                    {
                        Size = (uint)Marshal.SizeOf<NativeMethods.NotifyIconIdentifier>(),
                        WindowHandle = Handle,
                        Id = 1,
                        Guid = display.Id
                    };
                    if (NativeMethods.Shell_NotifyIconGetRect(ref identifier, out var rect) == 0 &&
                        rect.Contains(input.Point.X, input.Point.Y))
                    {
                        var totalDelta = _wheelRemainders.GetValueOrDefault(display.Index) + delta;
                        var notches = totalDelta / (int)WheelDelta;
                        _wheelRemainders[display.Index] = totalDelta - notches * (int)WheelDelta;
                        if (notches != 0)
                        {
                            NativeMethods.PostMessage(Handle, WheelMessage, (IntPtr)display.Index, (IntPtr)notches);
                        }

                        break;
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void HandleWheel(int index, int notches)
    {
        if (!_displayByIndex.TryGetValue(index, out var display))
        {
            return;
        }

        var screen = Screen.AllScreens.FirstOrDefault(item =>
            string.Equals(item.DeviceName, display.DeviceName, StringComparison.OrdinalIgnoreCase));
        if (screen is null)
        {
            return;
        }

        var increment = _settings.Increment;
        _ = Task.Run(() => _brightness.Adjust(screen, increment, notches))
            .ContinueWith(task =>
            {
                var status = task.IsFaulted
                    ? new BrightnessStatus(null, task.Exception!.GetBaseException().Message)
                    : task.Result;
                _statusUpdates.Enqueue((index, status));
                NativeMethods.PostMessage(Handle, RefreshMessage, IntPtr.Zero, IntPtr.Zero);
            }, TaskScheduler.Default);
    }

    private async Task RefreshBrightnessAsync()
    {
        if (_refreshing || _displays.Count == 0)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var snapshots = Entries.ToArray();
            var statusChanged = false;
            var results = await Task.WhenAll(snapshots.Select(async display =>
            {
                var screen = Screen.AllScreens.FirstOrDefault(item =>
                    string.Equals(item.DeviceName, display.DeviceName, StringComparison.OrdinalIgnoreCase));
                if (screen is null)
                {
                    return (display, new BrightnessStatus(null, "Display disconnected."));
                }

                return (display, await Task.Run(() => _brightness.Read(screen)));
            }));

            foreach (var (display, status) in results)
            {
                if (display.Status != status)
                {
                    display.Status = status;
                    UpdateIcon(display);
                    statusChanged = true;
                }
            }

            if (statusChanged)
            {
                _settingsForm?.UpdateDisplays(Entries);
            }
        }
        catch (Exception exception)
        {
            _settingsForm?.ShowError($"Could not refresh brightness: {exception.GetBaseException().Message}");
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings, _settingsPath, UpdateAllIcons, Exit);
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        }

        _settingsForm.UpdateDisplays(Entries);
        _settingsForm.Show();
        _settingsForm.WindowState = FormWindowState.Normal;
        _settingsForm.Activate();
    }

    private void UpdateAllIcons()
    {
        foreach (var display in Entries)
        {
            if (display.Icon is not null)
            {
                UpdateIcon(display);
            }
        }
    }

    private void ShowContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(Cursor.Position);
    }

    private void Exit()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        foreach (var display in Entries.ToArray())
        {
            RemoveIcon(display);
        }

        _displays.Clear();
        _displayByIndex.Clear();
        _settingsForm?.Dispose();
        _window.DestroyHandle();
        ExitThread();
    }

    private void HandleWindowMessage(ref Message message)
    {
        if (_taskbarCreatedMessage != 0 && message.Msg == _taskbarCreatedMessage)
        {
            foreach (var display in Entries)
            {
                AddIcon(display);
            }

            return;
        }

        if (message.Msg == NativeMethods.WmDisplayChange)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 700 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                RefreshDisplays();
            };
            timer.Start();
            return;
        }

        if (message.Msg == TrayCallbackMessage)
        {
            var eventId = unchecked((int)(message.LParam.ToInt64() & 0xFFFF));
            if (eventId is NativeMethods.WmLButtonUp or 0x0400 or 0x0401)
            {
                ShowSettings();
            }
            else if (eventId is NativeMethods.WmRButtonUp or NativeMethods.WmContextMenu)
            {
                ShowContextMenu();
            }

            return;
        }

        if (message.Msg == WheelMessage)
        {
            HandleWheel(unchecked((int)message.WParam.ToInt64()), unchecked((int)message.LParam.ToInt64()));
            return;
        }

        if (message.Msg == RefreshMessage)
        {
            while (_statusUpdates.TryDequeue(out var update))
            {
                if (!_displayByIndex.TryGetValue(update.Index, out var display))
                {
                    continue;
                }

                display.Status = update.Status;
                UpdateIcon(display);
            }

            _settingsForm?.UpdateDisplays(Entries);
        }
    }

    private sealed class TrayWindow(TrayApplicationContext owner) : NativeWindow
    {
        protected override void WndProc(ref Message message)
        {
            owner.HandleWindowMessage(ref message);
            base.WndProc(ref message);
        }
    }

    private static class Cursor
    {
        internal static Point Position
        {
            get
            {
                var point = new NativeMethods.Point();
                NativeMethods.GetCursorPos(ref point);
                return new Point(point.X, point.Y);
            }
        }
    }
}
