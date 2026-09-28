using SkiaSharp;
using SignalHouse.Core.Models;

namespace SignalHouse.Core.Imaging;

/// <summary>
/// A single open image: its full-resolution source pixels, a downsampled
/// preview bitmap for interactive editing, and the current EditState.
/// Editors work against the preview bitmap for responsiveness (see
/// <see cref="RenderPreview"/>, and the canvas control's own direct-draw
/// path, which is faster still); <see cref="RenderFullResolution"/>
/// re-applies the same EditState to the full-resolution source for export.
/// </summary>
public sealed class ImageDocument : IDisposable
{
    /// <summary>
    /// Interactive editing happens against a preview no larger than this on
    /// its long edge. This is the same technique professional photo editors
    /// use to keep slider dragging smooth regardless of the source photo's
    /// actual megapixel count -- full resolution is only touched once, on
    /// export.
    /// </summary>
    private const int MaxPreviewLongEdge = 2048;

    public SKBitmap FullResolutionSource { get; }
    public SKBitmap PreviewSource { get; }
    public EditState CurrentEdit { get; set; }
    public string? FilePath { get; }

    public ImageDocument(SKBitmap fullResolutionSource, string? filePath, EditState? initialEdit = null)
    {
        FullResolutionSource = fullResolutionSource;
        FilePath = filePath;
        CurrentEdit = initialEdit ?? EditState.Default();
        PreviewSource = BuildPreview(fullResolutionSource);
    }

    private static SKBitmap BuildPreview(SKBitmap source)
    {
        var longEdge = Math.Max(source.Width, source.Height);
        if (longEdge <= MaxPreviewLongEdge)
        {
            return source.Copy();
        }

        var scale = MaxPreviewLongEdge / (double)longEdge;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        var preview = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(preview))
        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High })
        {
            canvas.DrawBitmap(source, new SKRect(0, 0, width, height), paint);
        }

        return preview;
    }

    /// <summary>
    /// Renders the preview bitmap with the current EditState applied. Used
    /// for thumbnails and for any host surface that isn't drawing through
    /// the ImageCanvas control's own direct-draw path (which is faster,
    /// since it skips this intermediate bitmap entirely).
    /// </summary>
    public SKBitmap RenderPreview() => Render(PreviewSource);

    /// <summary>
    /// Renders the full-resolution source with the current EditState
    /// applied, for export/save. Slower than RenderPreview -- call this only
    /// when actually exporting.
    /// </summary>
    public SKBitmap RenderFullResolution() => Render(FullResolutionSource);

    private SKBitmap Render(SKBitmap source)
    {
        var result = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint { ImageFilter = ColorAdjustmentEngine.BuildImageFilter(CurrentEdit) };
        canvas.DrawBitmap(source, 0, 0, paint);
        return result;
    }

    public void Dispose()
    {
        FullResolutionSource.Dispose();
        PreviewSource.Dispose();
    }
}
