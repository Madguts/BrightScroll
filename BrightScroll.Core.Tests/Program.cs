using BrightScroll.Core;

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

var settingsPath = Path.Combine(Path.GetTempPath(), $"BrightScroll-{Guid.NewGuid():N}", "settings.json");
try
{
    var settings = new AppSettings { FontFamily = "Arial", TextColor = "#123ABC", Increment = 7 };
    settings.Save(settingsPath);
    var loaded = AppSettings.Load(settingsPath);
    if (loaded.FontFamily != "Arial" || loaded.TextColor != "#123ABC" || loaded.Increment != 7)
    {
        throw new InvalidOperationException("Settings did not round-trip.");
    }

    loaded.Increment = 0;
    loaded.TextColor = "not a color";
    loaded.Validate();
    if (loaded.Increment != 5 || loaded.TextColor != "#FFFFFF")
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

Console.WriteLine($"Passed {cases.Length} brightness cases and settings persistence checks.");
