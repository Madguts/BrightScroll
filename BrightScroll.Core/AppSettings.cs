using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrightScroll.Core;

public sealed class DisplayAppearance
{
    public string FontFamily { get; set; } = "Segoe UI";
    public string TextColor { get; set; } = "#FFFFFF";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            FontFamily = "Segoe UI";
        }

        if (!AppSettings.IsHtmlColor(TextColor))
        {
            TextColor = "#FFFFFF";
        }
    }
}

public sealed class AppSettings
{
    public int Increment { get; set; } = 5;
    public Dictionary<string, DisplayAppearance> DisplayAppearances { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    private string? LegacyFontFamily { get; set; }

    [JsonIgnore]
    private string? LegacyTextColor { get; set; }

    public void Validate()
    {
        if (Increment is < 1 or > 100)
        {
            Increment = 5;
        }

        if (DisplayAppearances is null)
        {
            DisplayAppearances = new Dictionary<string, DisplayAppearance>(StringComparer.OrdinalIgnoreCase);
        }
        else if (DisplayAppearances.Comparer != StringComparer.OrdinalIgnoreCase)
        {
            DisplayAppearances = new Dictionary<string, DisplayAppearance>(
                DisplayAppearances,
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (var identity in DisplayAppearances.Keys.ToArray())
        {
            if (DisplayAppearances[identity] is not { } appearance)
            {
                DisplayAppearances[identity] = new DisplayAppearance();
                continue;
            }

            appearance.Validate();
        }
    }

    public DisplayAppearance GetAppearance(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            throw new ArgumentException("A stable display identity is required.", nameof(identity));
        }

        Validate();
        if (!DisplayAppearances.TryGetValue(identity, out var appearance))
        {
            appearance = new DisplayAppearance
            {
                FontFamily = LegacyFontFamily ?? "Segoe UI",
                TextColor = LegacyTextColor ?? "#FFFFFF"
            };
            appearance.Validate();
            DisplayAppearances.Add(identity, appearance);
        }

        return appearance;
    }

    public AppSettings CreateSnapshot()
    {
        var snapshot = new AppSettings { Increment = Increment };
        foreach (var (identity, appearance) in DisplayAppearances)
        {
            snapshot.DisplayAppearances.Add(identity, new DisplayAppearance
            {
                FontFamily = appearance.FontFamily,
                TextColor = appearance.TextColor
            });
        }

        snapshot.LegacyFontFamily = LegacyFontFamily;
        snapshot.LegacyTextColor = LegacyTextColor;
        return snapshot;
    }

    public void Restore(AppSettings snapshot)
    {
        Increment = snapshot.Increment;
        foreach (var identity in DisplayAppearances.Keys
                     .Where(identity => !snapshot.DisplayAppearances.ContainsKey(identity))
                     .ToArray())
        {
            DisplayAppearances.Remove(identity);
        }

        foreach (var (identity, savedAppearance) in snapshot.DisplayAppearances)
        {
            if (DisplayAppearances.TryGetValue(identity, out var currentAppearance))
            {
                currentAppearance.FontFamily = savedAppearance.FontFamily;
                currentAppearance.TextColor = savedAppearance.TextColor;
            }
            else
            {
                DisplayAppearances.Add(identity, new DisplayAppearance
                {
                    FontFamily = savedAppearance.FontFamily,
                    TextColor = savedAppearance.TextColor
                });
            }
        }

        LegacyFontFamily = snapshot.LegacyFontFamily;
        LegacyTextColor = snapshot.LegacyTextColor;
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
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            settings.LegacyFontFamily = ReadLegacyString(document.RootElement, "FontFamily");
            settings.LegacyTextColor = ReadLegacyString(document.RootElement, "TextColor");
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

    internal static bool IsHtmlColor(string? value)
    {
        return value is { Length: 7 } &&
            value[0] == '#' &&
            value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    }

    private static string? ReadLegacyString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}
