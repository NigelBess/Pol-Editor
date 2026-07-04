using System.IO;
using System.Text.Json;

namespace PolFileEditor.Services;

/// <summary>
/// Small persisted settings blob (JSON in the user's AppData) so the editor can reopen
/// the last file on startup.
/// </summary>
public sealed class AppSettings
{
    public string? LastFilePath { get; set; }

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PolFileEditor");

    private static string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt or unreadable settings should never block startup.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Persisting settings is best-effort.
        }
    }
}
