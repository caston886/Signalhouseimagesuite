using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SignalHouse.App.Services;
using SignalHouse.Core.Imaging;
using SignalHouse.Core.Models;

namespace SignalHouse.App.ViewModels;

/// <summary>
/// Creates and enhances passport/visa photos: crop to a compliant size at
/// the correct DPI, replace the backdrop with a solid compliant color, and
/// apply the same shared exposure/color/clarity adjustments as the other two
/// editors.
///
/// Crop and canvas-navigation are deliberately independent: PanX/PanY/Zoom
/// (from EditorViewModelBase) are just for looking closely at the photo
/// while adjusting color, while CropCenterX/CropCenterY/CropZoom define the
/// actual output crop in normalized (0..1) source-image coordinates, which
/// makes the crop resolution-independent (the same normalized rectangle is
/// valid for both the on-screen preview bitmap and the full-resolution
/// export source) and immune to any mismatch between the two.
/// </summary>
public partial class PassportEditorViewModel : EditorViewModelBase, ICropOverlayProvider
{
    private const string FaceModelRelativePath = "Models/face_detector.onnx";
    private const string SegmentationModelRelativePath = "Models/portrait_matting.onnx";
    private static readonly string[] OpenExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };

    private IFaceDetector? _faceDetector;
    private bool _faceDetectorLoadAttempted;
    private IBackgroundRemover? _segmentationRemover;
    private bool _segmentationLoadAttempted;

    public PassportEditorViewModel() : base("Passport")
    {
    }

    public ObservableCollection<PassportSizePreset> SizePresets { get; } = new(PassportSizePreset.All);

    [ObservableProperty]
    private PassportSizePreset _selectedSizePreset = PassportSizePreset.Jamaica;

    [ObservableProperty]
    private double _exportDpi = 300;

    // --- Crop, independent of the shared view-navigation pan/zoom ------------
    [ObservableProperty] private double _cropCenterX = 0.5;
    [ObservableProperty] private double _cropCenterY = 0.42;
    [ObservableProperty] private double _cropZoom = 1.0;

    partial void OnSelectedSizePresetChanged(PassportSizePreset value) => RaisePreviewInvalidated();
    partial void OnCropCenterXChanged(double value) => RaisePreviewInvalidated();
    partial void OnCropCenterYChanged(double value) => RaisePreviewInvalidated();
    partial void OnCropZoomChanged(double value) => RaisePreviewInvalidated();

    public SKRect? CropOverlayNormalized => Document is null
        ? null
        : ComputeNormalizedCropRect((double)Document.PreviewSource.Width / Document.PreviewSource.Height);

    /// <summary>
    /// The largest rectangle of the selected preset's aspect ratio that fits
    /// inside the source image, scaled down by <see cref="CropZoom"/> and
    /// centered at (<see cref="CropCenterX"/>, <see cref="CropCenterY"/>),
    /// expressed as a fraction of the source image's own width/height.
    /// Valid for any bitmap that shares the source's aspect ratio (i.e. both
    /// the downsampled preview and the full-resolution original).
    /// </summary>
    public SKRect ComputeNormalizedCropRect(double sourceAspect)
    {
        var targetAspect = SelectedSizePreset.WidthMm / SelectedSizePreset.HeightMm;

        double baseWidthNorm, baseHeightNorm;
        if (sourceAspect > targetAspect)
        {
            baseHeightNorm = 1.0;
            baseWidthNorm = targetAspect / sourceAspect;
        }
        else
        {
            baseWidthNorm = 1.0;
            baseHeightNorm = sourceAspect / targetAspect;
        }

        var cropWidthNorm = Math.Clamp(baseWidthNorm / CropZoom, 0.02, 1.0);
        var cropHeightNorm = Math.Clamp(baseHeightNorm / CropZoom, 0.02, 1.0);

        var centerX = Math.Clamp(CropCenterX, cropWidthNorm / 2, 1 - cropWidthNorm / 2);
        var centerY = Math.Clamp(CropCenterY, cropHeightNorm / 2, 1 - cropHeightNorm / 2);

        return new SKRect(
            (float)(centerX - cropWidthNorm / 2),
            (float)(centerY - cropHeightNorm / 2),
            (float)(centerX + cropWidthNorm / 2),
            (float)(centerY + cropHeightNorm / 2));
    }

    // --- Background removal ----------------------------------------------------
    [ObservableProperty] private bool _removeBackground = true;
    [ObservableProperty] private bool _useAutomaticSegmentation;
    [ObservableProperty] private SKColor _keyColor = ChromaKeyBackgroundRemover.CommonGreenScreen;
    [ObservableProperty] private SKColor _replacementColor = SKColors.White;

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await FilePickerService.OpenFileAsync("Open portrait photo", OpenExtensions);
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

        LoadDocument(new ImageDocument(bitmap, path));
        RaisePreviewInvalidated(); // refresh the crop overlay against the new image's aspect ratio
    }

    /// <summary>
    /// Centers the crop on the detected face and sets CropZoom so the head
    /// fills a reasonable fraction of the frame. Requires a face-detection
    /// ONNX model at Models/face_detector.onnx next to the executable (see
    /// OnnxFaceDetector.cs) -- without one, this reports that manual framing
    /// (dragging the crop guide) is the way to go, since that path needs no
    /// model file at all.
    /// </summary>
    [RelayCommand]
    private void AutoCenterOnFace()
    {
        if (Document is null)
        {
            return;
        }

        var detector = GetFaceDetectorIfAvailable();
        if (detector is null)
        {
            StatusMessage = "No face-detection model found (Models/face_detector.onnx) -- use manual crop framing instead.";
            return;
        }

        var faces = detector.Detect(Document.PreviewSource);
        var best = faces.OrderByDescending(f => f.Confidence).FirstOrDefault();
        if (best is null)
        {
            StatusMessage = "No face detected -- use manual crop framing instead.";
            return;
        }

        var previewWidth = Document.PreviewSource.Width;
        var previewHeight = Document.PreviewSource.Height;
        var faceCenterX = (best.BoundingBox.Left + best.BoundingBox.Right) / 2 / previewWidth;
        var faceCenterY = (best.BoundingBox.Top + best.BoundingBox.Bottom) / 2 / previewHeight;

        CropCenterX = faceCenterX;
        // Bias the crop center slightly above the face's own center, since
        // passport guides want room for hair/shoulders below the chin, not a
        // perfectly face-centered square.
        CropCenterY = Math.Clamp(faceCenterY - 0.05, 0, 1);

        var faceHeightNorm = (best.BoundingBox.Bottom - best.BoundingBox.Top) / previewHeight;
        var (minPx, maxPx) = SelectedSizePreset.ComputeHeadHeightRangePx(ExportDpi);
        var (_, outputHeightPx) = SelectedSizePreset.ComputePixelSize(ExportDpi);
        var targetHeadFraction = (minPx + maxPx) / 2.0 / outputHeightPx;

        if (faceHeightNorm > 0 && targetHeadFraction > 0)
        {
            CropZoom = Math.Clamp(targetHeadFraction / faceHeightNorm, 0.2, 5.0);
        }

        StatusMessage = $"Centered on detected face (confidence {best.Confidence:P0}).";
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Document is null)
        {
            return;
        }

        var path = await FilePickerService.SaveFileAsync("Export passport photo", "passport.jpg", new[] { ".jpg", ".png" });
        if (path is null)
        {
            return;
        }

        using var adjusted = Document.RenderFullResolution();
        var normalizedCrop = ComputeNormalizedCropRect((double)adjusted.Width / adjusted.Height);
        var cropRectPx = new SKRectI(
            (int)(normalizedCrop.Left * adjusted.Width),
            (int)(normalizedCrop.Top * adjusted.Height),
            (int)(normalizedCrop.Right * adjusted.Width),
            (int)(normalizedCrop.Bottom * adjusted.Height));

        using var cropped = new SKBitmap(cropRectPx.Width, cropRectPx.Height);
        if (!adjusted.ExtractSubset(cropped, cropRectPx))
        {
            StatusMessage = "Failed to crop the image -- try adjusting the crop framing and export again.";
            return;
        }

        var (outputWidthPx, outputHeightPx) = SelectedSizePreset.ComputePixelSize(ExportDpi);
        using var resized = cropped.Resize(new SKImageInfo(outputWidthPx, outputHeightPx), SKFilterQuality.High);

        SKBitmap final = resized;
        SKBitmap? backgroundRemoved = null;
        if (RemoveBackground)
        {
            backgroundRemoved = ApplyBackgroundRemoval(resized);
            final = backgroundRemoved;
        }

        using var image = SKImage.FromBitmap(final);
        var format = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        using var encoded = image.Encode(format, 95);
        using var stream = File.OpenWrite(path);
        encoded.SaveTo(stream);

        backgroundRemoved?.Dispose();
        StatusMessage = $"Exported {SelectedSizePreset.Name} photo to {Path.GetFileName(path)}.";
    }

    private SKBitmap ApplyBackgroundRemoval(SKBitmap source)
    {
        var segmentationRemover = UseAutomaticSegmentation ? GetSegmentationRemoverIfAvailable() : null;

        if (segmentationRemover is not null)
        {
            using var mask = segmentationRemover.ComputeMask(source);
            return CompositeWithMask(source, mask, ReplacementColor);
        }

        // Default path: chroma key against the configured key color. Needs
        // no model file, so this is what runs whenever automatic
        // segmentation is off or no model was found -- the right default for
        // the common studio-backdrop passport photo shoot.
        var chromaKey = new ChromaKeyBackgroundRemover(KeyColor);
        using var chromaMask = chromaKey.ComputeMask(source);
        return chromaKey.ApplyWithSpillSuppression(source, chromaMask, ReplacementColor);
    }

    private static SKBitmap CompositeWithMask(SKBitmap source, SKBitmap mask, SKColor background)
    {
        var result = new SKBitmap(source.Width, source.Height);
        var srcPixels = source.Pixels;
        var maskPixels = mask.Pixels;
        var outPixels = new SKColor[srcPixels.Length];

        for (var i = 0; i < srcPixels.Length; i++)
        {
            var alpha = maskPixels[i].Red / 255f;
            var s = srcPixels[i];
            byte Blend(byte fg, byte bg) => (byte)Math.Round(fg * alpha + bg * (1 - alpha));
            outPixels[i] = new SKColor(Blend(s.Red, background.Red), Blend(s.Green, background.Green), Blend(s.Blue, background.Blue), 255);
        }

        result.Pixels = outPixels;
        return result;
    }

    private IFaceDetector? GetFaceDetectorIfAvailable()
    {
        if (_faceDetectorLoadAttempted)
        {
            return _faceDetector;
        }

        _faceDetectorLoadAttempted = true;
        var path = Path.Combine(AppContext.BaseDirectory, FaceModelRelativePath);
        if (File.Exists(path))
        {
            try
            {
                _faceDetector = new OnnxFaceDetector(path);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Found a face model but failed to load it: {ex.Message}";
            }
        }

        return _faceDetector;
    }

    private IBackgroundRemover? GetSegmentationRemoverIfAvailable()
    {
        if (_segmentationLoadAttempted)
        {
            return _segmentationRemover;
        }

        _segmentationLoadAttempted = true;
        var path = Path.Combine(AppContext.BaseDirectory, SegmentationModelRelativePath);
        if (File.Exists(path))
        {
            try
            {
                _segmentationRemover = new OnnxBackgroundRemover(path);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Found a segmentation model but failed to load it: {ex.Message}";
            }
        }

        return _segmentationRemover;
    }
}
