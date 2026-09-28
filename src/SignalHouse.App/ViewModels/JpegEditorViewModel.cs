using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SignalHouse.App.Services;
using SignalHouse.Core.Imaging;

namespace SignalHouse.App.ViewModels;

/// <summary>
/// The general-purpose JPEG editor: open any JPEG (or PNG/BMP/WEBP -- Skia
/// decodes all of these through the same call), edit with the shared
/// adjustment sliders, save back out.
/// </summary>
public partial class JpegEditorViewModel : EditorViewModelBase
{
    private static readonly string[] OpenExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

    public JpegEditorViewModel() : base("JPEG")
    {
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await FilePickerService.OpenFileAsync("Open image", OpenExtensions);
        if (path is null)
        {
            return;
        }

        var bitmap = SKBitmap.Decode(path);
        if (bitmap is null)
        {
            StatusMessage = "Could not decode that file.";
            return;
        }

        LoadDocumentFromBitmap(bitmap, path);
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (Document is null)
        {
            return;
        }

        var path = await FilePickerService.SaveFileAsync("Save image", "edited.jpg", new[] { ".jpg", ".png" });
        if (path is null)
        {
            return;
        }

        using var rendered = Document.RenderFullResolution();
        using var image = SKImage.FromBitmap(rendered);
        var format = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        using var encoded = image.Encode(format, 95);
        using var stream = File.OpenWrite(path);
        encoded.SaveTo(stream);

        StatusMessage = $"Saved to {Path.GetFileName(path)}.";
    }

    private void LoadDocumentFromBitmap(SKBitmap bitmap, string path)
    {
        var document = new ImageDocument(bitmap, path);
        LoadDocument(document);
    }
}
