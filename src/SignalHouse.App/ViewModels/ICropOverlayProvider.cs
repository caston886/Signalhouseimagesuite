using SkiaSharp;

namespace SignalHouse.App.ViewModels;

/// <summary>
/// Implemented by an editor view model that wants ImageCanvas to draw a crop
/// guide overlay on top of the image, in the same transformed coordinate
/// space as the image itself (so it stays glued to the photo under pan/zoom/
/// rotate). Only PassportEditorViewModel implements this today.
/// </summary>
public interface ICropOverlayProvider
{
    /// <summary>The crop rectangle in normalized (0..1) source-image coordinates, or null to draw no overlay.</summary>
    SKRect? CropOverlayNormalized { get; }
}
