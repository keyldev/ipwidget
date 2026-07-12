using System;
using System.IO;
using System.Text.Json;

namespace IpWidget.Services;

/// <summary>Persisted user preferences, stored as JSON in %AppData%/IpWidget.</summary>
public sealed class Settings
{
    public bool CloseToTray { get; set; } = true;
    public bool Compact { get; set; }
    public bool Topmost { get; set; }
    public bool ShowFlag { get; set; } = true;
    public bool AutoRefresh { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IpWidget");
    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* corrupt / unreadable -> defaults */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}
