using System.Text.Json;
using SignalHouse.Core.Models;

namespace SignalHouse.Core.Presets;

/// <summary>
/// Loads and saves EditPresets to a single shared folder on disk so that a
/// template created in one editor is immediately visible in the others --
/// this is the "universal preset/template system" requirement. Each preset
/// is its own JSON file, named by its Id, so saving, renaming, and deleting
/// never requires rewriting a shared index file.
/// </summary>
public sealed class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string PresetsDirectory { get; }

    public PresetStore(string? presetsDirectory = null)
    {
        PresetsDirectory = presetsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SignalHouseImagingSuite",
            "Presets");

        Directory.CreateDirectory(PresetsDirectory);
    }

    public IReadOnlyList<EditPreset> LoadAll()
    {
        var results = new List<EditPreset>();

        foreach (var file in Directory.EnumerateFiles(PresetsDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var preset = JsonSerializer.Deserialize<EditPreset>(json, JsonOptions);
                if (preset is not null)
                {
                    results.Add(preset);
                }
            }
            catch (JsonException)
            {
                // Skip a corrupt preset file rather than failing the whole load --
                // one bad file on disk shouldn't take out the entire preset list.
            }
        }

        return results
            .OrderByDescending(p => p.CreatedUtc)
            .ToList();
    }

    public void Save(EditPreset preset)
    {
        var path = Path.Combine(PresetsDirectory, $"{preset.Id}.json");
        var json = JsonSerializer.Serialize(preset, JsonOptions);
        File.WriteAllText(path, json);
    }

    public void Delete(EditPreset preset)
    {
        var path = Path.Combine(PresetsDirectory, $"{preset.Id}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
