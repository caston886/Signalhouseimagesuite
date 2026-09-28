namespace SignalHouse.Core.Models;

/// <summary>
/// A named, saved EditState -- a "template" in the product's language.
/// Presets are stored as plain JSON (see PresetStore) in one shared folder,
/// so a preset saved from the JPEG editor shows up in the RAW editor's
/// preset list and vice versa.
/// </summary>
public sealed class EditPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Untitled Preset";

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Which editor this preset was originally created in ("Passport", "JPEG", or "RAW"). Informational only -- it does not restrict where the preset can be applied.</summary>
    public string SourceEditor { get; set; } = "Unknown";

    public EditState State { get; set; } = EditState.Default();
}
