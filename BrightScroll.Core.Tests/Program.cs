using BrightScroll.Core;

var monitorIdentity = "device-id:DISPLAY\\ACME1234\\SERIAL";
var iconId = NotificationIconIdentity.Create(monitorIdentity);
if (iconId != NotificationIconIdentity.Create(monitorIdentity.ToLowerInvariant()) ||
    iconId == NotificationIconIdentity.Create("device-id:DISPLAY\\ACME1234\\OTHER"))
{
    throw new InvalidOperationException("Notification icon identity is not stable and monitor-specific.");
}

var cases = new (int Current, int Increment, int Notches, int Expected)[]
{
    (50, 5, 1, 55),
    (50, 5, -1, 45),
    (98, 5, 1, 100),
    (2, 5, -1, 0),
    (50, 5, 3, 65),
    (50, 100, int.MaxValue, 100),
    (50, 100, int.MinValue, 0)
};

foreach (var (current, increment, notches, expected) in cases)
{
    var actual = BrightnessMath.Adjust(current, increment, notches);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Adjust({current}, {increment}, {notches}) returned {actual}, expected {expected}.");
    }
}

var retryDelays = new[] { 5, 10, 20, 40, 60, 60 };
for (var index = 0; index < retryDelays.Length; index++)
{
    var actual = TrayIconRetryDelay.GetDelay(index + 1);
    if (actual != TimeSpan.FromSeconds(retryDelays[index]))
    {
        throw new InvalidOperationException(
            $"Tray icon retry {index + 1} returned {actual.TotalSeconds}s, expected {retryDelays[index]}s.");
    }
}

try
{
    TrayIconRetryDelay.GetDelay(0);
    throw new InvalidOperationException("A zero-failure tray retry count was accepted.");
}
catch (ArgumentOutOfRangeException)
{
}

var settingsPath = Path.Combine(Path.GetTempPath(), $"BrightScroll-{Guid.NewGuid():N}", "settings.json");
try
{
    var settings = new AppSettings { Increment = 7 };
    var firstAppearance = settings.GetAppearance("monitor-a");
    firstAppearance.FontFamily = "Arial";
    firstAppearance.TextColor = "#123ABC";
    var secondAppearance = settings.GetAppearance("monitor-b");
    secondAppearance.FontFamily = "Consolas";
    secondAppearance.TextColor = "#ABCDEF";
    settings.Save(settingsPath);
    var loaded = AppSettings.Load(settingsPath);
    if (loaded.GetAppearance("MONITOR-A").FontFamily != "Arial" ||
        loaded.GetAppearance("monitor-a").TextColor != "#123ABC" ||
        loaded.GetAppearance("monitor-b").FontFamily != "Consolas" ||
        loaded.Increment != 7)
    {
        throw new InvalidOperationException("Per-display settings did not round-trip by stable identity.");
    }

    var savedJson = File.ReadAllText(settingsPath);
    using (var settingsDocument = System.Text.Json.JsonDocument.Parse(savedJson))
    {
        var root = settingsDocument.RootElement;
        if (root.TryGetProperty("BackgroundColor", out _) ||
            root.TryGetProperty("FontFamily", out _) ||
            root.TryGetProperty("TextColor", out _))
        {
            throw new InvalidOperationException("Removed global appearance settings were written to disk.");
        }
    }

    var legacyPath = Path.Combine(Path.GetDirectoryName(settingsPath)!, "legacy-settings.json");
    File.WriteAllText(legacyPath, """
        {
          "FontFamily": "Arial",
          "TextColor": "#123ABC",
          "BackgroundColor": "#101010",
          "Increment": 9
        }
        """);
    var migrated = AppSettings.Load(legacyPath);
    var migratedAppearance = migrated.GetAppearance("stable-monitor-id");
    if (migrated.Increment != 9 ||
        migratedAppearance.FontFamily != "Arial" ||
        migratedAppearance.TextColor != "#123ABC")
    {
        throw new InvalidOperationException("Legacy settings were not migrated while preserving the increment.");
    }

    migrated.Save(legacyPath);
    if (File.ReadAllText(legacyPath).Contains("BackgroundColor", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Legacy background settings were not removed on save.");
    }

    loaded.Increment = 0;
    loaded.GetAppearance("monitor-a").TextColor = "not a color";
    loaded.Validate();
    if (loaded.Increment != 5 || loaded.GetAppearance("monitor-a").TextColor != "#FFFFFF")
    {
        throw new InvalidOperationException("Invalid settings were not reset to safe defaults.");
    }
}
finally
{
    var directory = Path.GetDirectoryName(settingsPath)!;
    if (Directory.Exists(directory))
    {
        Directory.Delete(directory, recursive: true);
    }
}

Console.WriteLine(
    $"Passed {cases.Length} brightness cases, tray icon retry backoff, and settings migration/persistence checks.");
