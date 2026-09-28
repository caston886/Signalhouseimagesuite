using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SignalHouse.App.Services;
using SignalHouse.Core.Imaging;
using SignalHouse.Core.Imaging.RawDecoding;

namespace SignalHouse.App.ViewModels;

/// <summary>
/// The RAW editor: opens camera RAW files (CR2, NEF, ARW, DNG, and whatever
/// else LibRaw supports -- see LibRawDecoder.SupportedExtensions) and offers
/// both an Auto mode (AutoAdjustmentEstimator seeds exposure and white
/// balance from the image itself) and full Manual control via the same
/// shared sliders every other editor uses.
/// </summary>
public partial class RawEditorViewModel : EditorViewModelBase
{
    private readonly IRawDecoder _decoder = new LibRawDecoder();

    public RawEditorViewModel() : base("RAW")
    {
    }

    public IReadOnlyList<string> SupportedExtensions => _decoder.SupportedExtensions;

    [ObservableProperty]
    private bool _isAutoMode = true;

    [ObservableProperty]
    private string? _cameraInfo;

    partial void OnIsAutoModeChanged(bool value)
    {
        if (value && Document is not null)
        {
            ApplyAutoEstimate();
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await FilePickerService.OpenFileAsync("Open RAW file", _decoder.SupportedExtensions);
        if (path is null)
        {
            return;
        }

        StatusMessage = "Decoding RAW file...";

        RawDecodeResult decoded;
        try
        {
            // LibRaw decoding is CPU-bound and can take a second or two for
            // a high-megapixel file -- run it off the UI thread so the
            // window stays responsive.
            decoded = await Task.Run(() => _decoder.Decode(path));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to decode RAW file: {ex.Message}";
            return;
        }

        var bitmap = RawImageConverter.ToSKBitmap(decoded);
        var document = new ImageDocument(bitmap, path);
        LoadDocument(document);

        CameraInfo = string.IsNullOrWhiteSpace(decoded.CameraModel)
            ? null
            : $"{decoded.CameraMake} {decoded.CameraModel} · ISO {decoded.IsoSpeed:0} · f/{decoded.Aperture:0.0} · {FormatShutter(decoded.ShutterSeconds)}";

        if (IsAutoMode)
        {
            ApplyAutoEstimate();
        }
    }

    [RelayCommand]
    private void ApplyAutoEstimate()
    {
        if (Document is null)
        {
            return;
        }

        var estimate = AutoAdjustmentEstimator.EstimateFor(Document.PreviewSource);
        // Preserve any manual color-balance / clarity / rotation work already
        // done; Auto only ever seeds exposure and white balance.
        Document.CurrentEdit.Exposure = estimate.Exposure;
        Document.CurrentEdit.WhiteBalanceTemperature = estimate.WhiteBalanceTemperature;
        Document.CurrentEdit.WhiteBalanceTint = estimate.WhiteBalanceTint;

        // Re-load all sliders from the (partially updated) EditState so the
        // UI reflects the new exposure/white balance without disturbing
        // anything else.
        LoadCurrentEditIntoSliders();
        StatusMessage = "Applied automatic exposure and white balance.";
    }

    private void LoadCurrentEditIntoSliders()
    {
        if (Document is null)
        {
            return;
        }

        // Setting these public, bound properties pushes them back into
        // Document.CurrentEdit and raises PreviewInvalidated automatically
        // (see EditorViewModelBase.OnExposureChanged and friends).
        Exposure = Document.CurrentEdit.Exposure;
        WhiteBalanceTemperature = Document.CurrentEdit.WhiteBalanceTemperature;
        WhiteBalanceTint = Document.CurrentEdit.WhiteBalanceTint;
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (Document is null)
        {
            return;
        }

        var path = await FilePickerService.SaveFileAsync("Save image", "edited.jpg", new[] { ".jpg", ".png", ".tiff" });
        if (path is null)
        {
            return;
        }

        using var rendered = Document.RenderFullResolution();
        using var image = SKImage.FromBitmap(rendered);
        var format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            _ => SKEncodedImageFormat.Jpeg,
        };
        using var encoded = image.Encode(format, 95);
        using var stream = File.OpenWrite(path);
        encoded.SaveTo(stream);

        StatusMessage = $"Saved to {Path.GetFileName(path)}.";
    }

    private static string FormatShutter(double seconds)
    {
        if (seconds <= 0)
        {
            return "?";
        }

        return seconds >= 1 ? $"{seconds:0.#}s" : $"1/{Math.Round(1 / seconds)}s";
    }
}
