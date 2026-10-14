using System.Text.Json;
using System.IO;

namespace MicroPresets;

internal static class PresetStore
{
    private sealed record Settings(int SchemaVersion, Preset[] Presets);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    internal static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexMicroPresets");

    internal static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    internal static string LogPath => Path.Combine(DataDirectory, "runtime.jsonl");
    internal static string LayoutBackupPath => Path.Combine(DataDirectory, "layout-backup.json");

    internal static Preset[] Load()
    {
        if (!File.Exists(SettingsPath)) return Presets.Defaults();
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath), JsonOptions)
            ?? throw new InvalidDataException("The model assignments file is empty or damaged.");
        return settings.SchemaVersion switch
        {
            1 => Presets.MigrateLegacy(settings.Presets),
            2 => Presets.WithAssignments(settings.Presets),
            _ => throw new InvalidDataException("This model assignments file uses an unsupported version.")
        };
    }

    internal static void Save(IEnumerable<Preset> presets)
    {
        var valid = Presets.WithAssignments(presets);
        Directory.CreateDirectory(DataDirectory);
        string temporary = SettingsPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new Settings(2, valid), JsonOptions));
        File.Move(temporary, SettingsPath, overwrite: true);
    }
}
