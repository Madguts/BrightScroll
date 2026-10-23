using System.Text.Json;

namespace BrightScroll.Core;

public sealed class AppSettings
{
    public string FontFamily { get; set; } = "Segoe UI";
    public string TextColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "#202020";
    public int Increment { get; set; } = 5;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            FontFamily = "Segoe UI";
        }

        if (Increment is < 1 or > 100)
        {
            Increment = 5;
        }

        if (!IsHtmlColor(TextColor))
        {
            TextColor = "#FFFFFF";
        }

        if (!IsHtmlColor(BackgroundColor))
        {
            BackgroundColor = "#202020";
        }
    }

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
            settings.Validate();
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(string path)
    {
        Validate();
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The settings file must have a parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static bool IsHtmlColor(string? value)
    {
        return value is { Length: 7 } &&
            value[0] == '#' &&
            value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    }
}
