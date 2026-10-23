using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrightScroll.Windows;

internal sealed record BrightnessStatus(int? Percent, string Message)
{
    internal bool IsAvailable => Percent.HasValue;
}

internal sealed class DisplayBrightnessService
{
    private readonly object _adjustLock = new();

    internal BrightnessStatus Read(Screen screen)
    {
        var errors = new List<string>();
        var physical = ReadPhysicalMonitor(screen.DeviceName, errors);
        if (physical.HasValue)
        {
            return new BrightnessStatus(physical.Value, "DDC/CI");
        }

        var wmi = ReadWmiMonitor(screen.DeviceName, errors);
        if (wmi.HasValue)
        {
            return new BrightnessStatus(wmi.Value, "Windows monitor control");
        }

        return new BrightnessStatus(null, errors.Count == 0
            ? "Brightness control is not supported by this display."
            : string.Join(" ", errors.Distinct()));
    }

    internal BrightnessStatus Adjust(Screen screen, int increment, int notches)
    {
        lock (_adjustLock)
        {
            var current = Read(screen);
            if (!current.IsAvailable)
            {
                return current;
            }

            var target = (int)Math.Clamp((long)current.Percent!.Value + (long)increment * notches, 0, 100);
            var errors = new List<string>();
            if (SetPhysicalMonitor(screen.DeviceName, target, errors) ||
                SetWmiMonitor(screen.DeviceName, target, errors))
            {
                return new BrightnessStatus(target, current.Message);
            }

            return new BrightnessStatus(null, errors.Count == 0
                ? "The display did not accept the brightness change."
                : string.Join(" ", errors.Distinct()));
        }
    }

    private static int? ReadPhysicalMonitor(string deviceName, List<string> errors)
    {
        foreach (var monitor in FindMonitors(deviceName))
        {
            if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0)
            {
                continue;
            }

            var monitors = new NativeMethods.PhysicalMonitor[count];
            try
            {
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(monitor, count, monitors))
                {
                    errors.Add($"Could not query DDC/CI for {deviceName} (error {Marshal.GetLastWin32Error()}).");
                    continue;
                }

                foreach (var physical in monitors)
                {
                    if (NativeMethods.GetMonitorBrightness(physical.Handle, out var minimum, out var current, out var maximum) &&
                        maximum > minimum)
                    {
                        return Math.Clamp((int)Math.Round((current - minimum) * 100d / (maximum - minimum)), 0, 100);
                    }
                }
            }
            finally
            {
                NativeMethods.DestroyPhysicalMonitors(count, monitors);
            }
        }

        return null;
    }

    private static bool SetPhysicalMonitor(string deviceName, int percent, List<string> errors)
    {
        foreach (var monitor in FindMonitors(deviceName))
        {
            if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0)
            {
                continue;
            }

            var monitors = new NativeMethods.PhysicalMonitor[count];
            try
            {
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(monitor, count, monitors))
                {
                    errors.Add($"Could not query DDC/CI for {deviceName} (error {Marshal.GetLastWin32Error()}).");
                    continue;
                }

                foreach (var physical in monitors)
                {
                    if (!NativeMethods.GetMonitorBrightness(physical.Handle, out var minimum, out _, out var maximum) ||
                        maximum <= minimum)
                    {
                        continue;
                    }

                    var target = minimum + (uint)Math.Round(percent * (maximum - minimum) / 100d);
                    if (NativeMethods.SetMonitorBrightness(physical.Handle, target))
                    {
                        return true;
                    }

                    errors.Add($"The display rejected DDC/CI brightness control (error {Marshal.GetLastWin32Error()}).");
                }
            }
            finally
            {
                NativeMethods.DestroyPhysicalMonitors(count, monitors);
            }
        }

        return false;
    }

    private static int? ReadWmiMonitor(string deviceName, List<string> errors)
    {
        var instanceName = FindWmiInstanceName(deviceName, errors);
        if (instanceName is null)
        {
            return null;
        }

        try
        {
            using var searcher = CreateWmiSearcher(
                "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    if (InstanceNamesMatch(Convert.ToString(item["InstanceName"]), instanceName))
                    {
                        return Convert.ToInt32(item["CurrentBrightness"]);
                    }
                }
            }
        }
        catch (ManagementException exception)
        {
            errors.Add($"Windows monitor brightness query failed: {exception.Message}");
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException)
        {
            errors.Add($"Windows monitor brightness query failed: {exception.Message}");
        }

        return null;
    }

    private static bool SetWmiMonitor(string deviceName, int percent, List<string> errors)
    {
        var instanceName = FindWmiInstanceName(deviceName, errors);
        if (instanceName is null)
        {
            return false;
        }

        try
        {
            using var searcher = CreateWmiSearcher(
                "SELECT InstanceName FROM WmiMonitorBrightnessMethods WHERE Active = TRUE");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    if (!InstanceNamesMatch(Convert.ToString(item["InstanceName"]), instanceName))
                    {
                        continue;
                    }

                    using var parameters = item.GetMethodParameters("WmiSetBrightness");
                    parameters["Timeout"] = (uint)1;
                    parameters["Brightness"] = (byte)percent;
                    using var output = item.InvokeMethod("WmiSetBrightness", parameters, null);
                    if (output is not null && Convert.ToUInt32(output["ReturnValue"]) == 0)
                    {
                        return true;
                    }

                    errors.Add("Windows did not accept the internal display brightness change.");
                    return false;
                }
            }
        }
        catch (ManagementException exception)
        {
            errors.Add($"Windows monitor brightness update failed: {exception.Message}");
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException)
        {
            errors.Add($"Windows monitor brightness update failed: {exception.Message}");
        }

        return false;
    }

    private static ManagementObjectSearcher CreateWmiSearcher(string query)
    {
        return new ManagementObjectSearcher(
            new ManagementScope(@"\\.\root\WMI"),
            new ObjectQuery(query),
            new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(3) });
    }

    private static string? FindWmiInstanceName(string deviceName, List<string> errors)
    {
        var display = new NativeMethods.DisplayDevice
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.DisplayDevice>(),
            DeviceName = string.Empty,
            DeviceString = string.Empty,
            DeviceId = string.Empty,
            DeviceKey = string.Empty
        };
        if (!NativeMethods.EnumDisplayDevices(deviceName, 0, ref display, NativeMethods.EddGetDeviceInterfaceName) ||
            string.IsNullOrWhiteSpace(display.DeviceId))
        {
            return null;
        }

        var prefix = display.DeviceId;
        if (prefix.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            prefix = prefix[4..];
        }

        var interfaceSuffix = prefix.IndexOf("#{", StringComparison.Ordinal);
        if (interfaceSuffix >= 0)
        {
            prefix = prefix[..interfaceSuffix];
        }

        prefix = prefix.Replace('#', '\\');
        try
        {
            using var searcher = CreateWmiSearcher("SELECT InstanceName FROM WmiMonitorBrightness WHERE Active = TRUE");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var candidate = Convert.ToString(item["InstanceName"]);
                    if (candidate is not null && InstanceNamesMatch(candidate, prefix))
                    {
                        return candidate;
                    }
                }
            }
        }
        catch (ManagementException exception)
        {
            errors.Add($"Windows monitor discovery failed: {exception.Message}");
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException)
        {
            errors.Add($"Windows monitor discovery failed: {exception.Message}");
        }

        return null;
    }

    private static bool InstanceNamesMatch(string? instanceName, string? devicePrefix)
    {
        return instanceName is not null && devicePrefix is not null &&
            (instanceName.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase) ||
             devicePrefix.StartsWith(instanceName, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<IntPtr> FindMonitors(string deviceName)
    {
        var result = new List<IntPtr>();
        NativeMethods.MonitorEnumProcedure callback = (monitor, _, _, _) =>
        {
            var info = new NativeMethods.MonitorInfoEx
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                DeviceName = string.Empty
            };
            if (NativeMethods.GetMonitorInfo(monitor, ref info) &&
                string.Equals(info.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(monitor);
            }

            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return result;
    }
}
