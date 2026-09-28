using SkiaSharp;

namespace SignalHouse.Core.Imaging;

/// <summary>
/// Removes a solid studio backdrop (green, blue, white, or any other flat
/// color) by chroma-keying: pixels close to the chosen key color become
/// transparent, with a soft feathered edge so hair and shoulders don't get a
/// hard cutout line, and a spill-suppression pass that desaturates any
/// leftover key-color tint reflected onto the subject's edges (e.g. green
/// bounced onto hair from a green screen).
///
/// This needs no external model, network access, or GPU -- it's the fully
/// self-contained path for the common passport-photo studio setup (plain
/// colored backdrop). For arbitrary, non-studio backgrounds, see
/// <see cref="OnnxBackgroundRemover"/> instead.
/// </summary>
public sealed class ChromaKeyBackgroundRemover : IBackgroundRemover
{
    private readonly SKColor _keyColor;
    private readonly float _tolerance;
    private readonly float _feather;

    /// <param name="keyColor">The backdrop color to remove.</param>
    /// <param name="tolerance">Color distance (0..441, since it's a 3D RGB Euclidean distance) below which a pixel is treated as pure background.</param>
    /// <param name="feather">Additional distance over which the edge fades from transparent to opaque.</param>
    public ChromaKeyBackgroundRemover(SKColor keyColor, float tolerance = 40f, float feather = 25f)
    {
        _keyColor = keyColor;
        _tolerance = tolerance;
        _feather = feather;
    }

    public static readonly SKColor CommonGreenScreen = new(0, 177, 64);
    public static readonly SKColor CommonBlueScreen = new(0, 71, 187);

    public SKBitmap ComputeMask(SKBitmap source)
    {
        var mask = new SKBitmap(source.Width, source.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        var srcPixels = source.Pixels; // one managed copy -- far fewer native calls than per-pixel GetPixel
        var maskPixels = new SKColor[srcPixels.Length];

        for (var i = 0; i < srcPixels.Length; i++)
        {
            var distance = ColorDistance(srcPixels[i], _keyColor);

            byte alpha;
            if (distance <= _tolerance)
            {
                alpha = 0;
            }
            else if (distance >= _tolerance + _feather)
            {
                alpha = 255;
            }
            else
            {
                var t = (distance - _tolerance) / _feather;
                alpha = (byte)Math.Round(t * 255);
            }

            maskPixels[i] = new SKColor(alpha, alpha, alpha);
        }

        mask.Pixels = maskPixels;
        return mask;
    }

    /// <summary>
    /// Applies the mask as alpha, replaces fully-transparent pixels with
    /// <paramref name="replacementColor"/> (or leaves them fully transparent
    /// if you intend to composite onto something else later), and suppresses
    /// key-color spill on the remaining semi-transparent edge pixels.
    /// </summary>
    public SKBitmap ApplyWithSpillSuppression(SKBitmap source, SKBitmap mask, SKColor replacementColor)
    {
        var result = new SKBitmap(source.Width, source.Height);
        var srcPixels = source.Pixels;
        var maskPixels = mask.Pixels;
        var outPixels = new SKColor[srcPixels.Length];

        for (var i = 0; i < srcPixels.Length; i++)
        {
            var pixel = srcPixels[i];
            var alpha = maskPixels[i].Red; // Gray8 stores the value in the red channel

            var spillAmount = KeyChannelExcess(pixel);
            byte r = pixel.Red, g = pixel.Green, b = pixel.Blue;

            if (spillAmount > 0 && alpha > 0)
            {
                DesaturateTowardKeyAxis(ref r, ref g, ref b, spillAmount);
            }

            var subject = new SKColor(r, g, b, alpha);
            outPixels[i] = alpha == 255 ? subject : BlendOver(subject, replacementColor, alpha);
        }

        result.Pixels = outPixels;
        return result;
    }

    /// <summary>How dominant the key channel (green or blue) is over the average of the other two, 0..1. Zero for a non-green/blue key color.</summary>
    private float KeyChannelExcess(SKColor pixel)
    {
        if (_keyColor.Green >= _keyColor.Red && _keyColor.Green >= _keyColor.Blue)
        {
            return Math.Max(0, pixel.Green - (pixel.Red + pixel.Blue) / 2f) / 255f;
        }

        if (_keyColor.Blue >= _keyColor.Red && _keyColor.Blue >= _keyColor.Green)
        {
            return Math.Max(0, pixel.Blue - (pixel.Red + pixel.Green) / 2f) / 255f;
        }

        return 0f;
    }

    private void DesaturateTowardKeyAxis(ref byte r, ref byte g, ref byte b, float spillAmount)
    {
        if (_keyColor.Green >= _keyColor.Red && _keyColor.Green >= _keyColor.Blue)
        {
            var avg = (r + b) / 2f;
            g = (byte)Math.Clamp(g - spillAmount * (g - avg), 0, 255);
        }
        else if (_keyColor.Blue >= _keyColor.Red && _keyColor.Blue >= _keyColor.Green)
        {
            var avg = (r + g) / 2f;
            b = (byte)Math.Clamp(b - spillAmount * (b - avg), 0, 255);
        }
    }

    private static SKColor BlendOver(SKColor fg, SKColor bg, byte alpha)
    {
        var a = alpha / 255f;
        byte Blend(byte f, byte b) => (byte)Math.Round(f * a + b * (1 - a));
        return new SKColor(Blend(fg.Red, bg.Red), Blend(fg.Green, bg.Green), Blend(fg.Blue, bg.Blue), 255);
    }

    private static float ColorDistance(SKColor a, SKColor b)
    {
        var dr = a.Red - b.Red;
        var dg = a.Green - b.Green;
        var db = a.Blue - b.Blue;
        return MathF.Sqrt(dr * dr + dg * dg + db * db);
    }
}
