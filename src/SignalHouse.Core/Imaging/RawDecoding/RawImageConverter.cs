using SkiaSharp;

namespace SignalHouse.Core.Imaging.RawDecoding;

/// <summary>
/// Converts a decoded RAW image into an SKBitmap for the shared editing
/// pipeline. LibRaw's dcraw_process (run here with its default gamma/tone
/// curve parameters) already applies a standard display gamma to its
/// output, so truncating 16-bit-per-channel down to 8-bit is a safe
/// precision reduction, not a raw-to-linear conversion that would need its
/// own gamma math.
///
/// Editing math (<see cref="ColorAdjustmentEngine"/>) runs on 8-bit pixels
/// for interactive preview performance -- indistinguishable on screen, and
/// standard practice even in professional tools, which typically keep full
/// bit depth only for the final render. The RAW decode itself keeps full
/// 16-bit precision (<see cref="RawDecodeResult.Rgb16"/>) so a future
/// higher-precision export path has real headroom to draw on.
/// </summary>
public static class RawImageConverter
{
    public static SKBitmap ToSKBitmap(RawDecodeResult raw)
    {
        var bitmap = new SKBitmap(raw.Width, raw.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new SKColor[raw.Width * raw.Height];

        for (var i = 0; i < pixels.Length; i++)
        {
            var srcIndex = i * 3;
            var r = (byte)(raw.Rgb16[srcIndex] >> 8);
            var g = (byte)(raw.Rgb16[srcIndex + 1] >> 8);
            var b = (byte)(raw.Rgb16[srcIndex + 2] >> 8);
            pixels[i] = new SKColor(r, g, b);
        }

        bitmap.Pixels = pixels;
        return bitmap;
    }
}
