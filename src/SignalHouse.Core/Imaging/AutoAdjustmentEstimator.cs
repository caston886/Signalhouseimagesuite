using SkiaSharp;
using SignalHouse.Core.Models;
using SignalHouse.Core.Utilities;

namespace SignalHouse.Core.Imaging;

/// <summary>
/// Computes a starting EditState for the "Auto" side of each editor's
/// Auto/Manual toggle: an automatic white balance (gray-world assumption --
/// average scene color is assumed to be neutral gray, then a color
/// temperature is picked to correct toward that) and an automatic exposure
/// (targets a mid-gray average luminance). The user's Manual controls apply
/// on top of -- or instead of -- whatever this estimates; nothing here is
/// destructive or final.
/// </summary>
public static class AutoAdjustmentEstimator
{
    private const int SampleLongEdge = 256; // downsample for speed; a few hundred pixels is plenty for a global average

    public static EditState EstimateFor(SKBitmap source)
    {
        using var sample = Downsample(source);
        var (avgR, avgG, avgB) = AverageColor(sample);

        var state = EditState.Default();
        state.WhiteBalanceTemperature = EstimateTemperature(avgR, avgG, avgB);
        state.WhiteBalanceTint = EstimateTint(avgR, avgG, avgB);
        state.Exposure = EstimateExposureStops(sample);
        return state;
    }

    private static SKBitmap Downsample(SKBitmap source)
    {
        var longEdge = Math.Max(source.Width, source.Height);
        if (longEdge <= SampleLongEdge)
        {
            return source.Copy();
        }

        var scale = SampleLongEdge / (double)longEdge;
        var width = Math.Max(1, (int)(source.Width * scale));
        var height = Math.Max(1, (int)(source.Height * scale));
        return source.Resize(new SKImageInfo(width, height), SKFilterQuality.Low);
    }

    private static (double R, double G, double B) AverageColor(SKBitmap bitmap)
    {
        double sumR = 0, sumG = 0, sumB = 0;
        var pixels = bitmap.Pixels;

        foreach (var pixel in pixels)
        {
            sumR += pixel.Red;
            sumG += pixel.Green;
            sumB += pixel.Blue;
        }

        var count = Math.Max(1, pixels.Length);
        return (sumR / count, sumG / count, sumB / count);
    }

    /// <summary>
    /// Gray-world white balance: searches candidate Kelvin values for the one
    /// whose black-body color cast, when corrected for, would bring the
    /// image's average R/B ratio closest to neutral (1:1).
    /// </summary>
    private static double EstimateTemperature(double avgR, double avgG, double avgB)
    {
        var targetRatio = avgR / Math.Max(1, avgB); // the image's own observed red/blue cast

        var bestKelvin = 6500.0;
        var bestDelta = double.MaxValue;

        for (var kelvin = 2000.0; kelvin <= 12000.0; kelvin += 50.0)
        {
            var (r, _, b) = BlackBodyRadiator.ToRgb255(kelvin);
            var castRatio = r / b; // how a light source at this Kelvin biases R relative to B

            // If the scene were lit by a source at `kelvin`, its cast ratio
            // should match the image's observed ratio -- the Kelvin that
            // minimizes this gap is our estimate of the actual light source.
            var delta = Math.Abs(castRatio - targetRatio);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestKelvin = kelvin;
            }
        }

        return bestKelvin;
    }

    private static double EstimateTint(double avgR, double avgG, double avgB)
    {
        // Green/magenta axis: compare green against the average of red and
        // blue. A green-heavy average (common under fluorescent lighting)
        // suggests a magenta correction (negative tint moves toward green
        // per this app's WhiteBalanceTint convention -- see EditState).
        var neutral = (avgR + avgB) / 2.0;
        var greenExcess = avgG - neutral; // positive = green cast

        // Map to the -100..100 slider range with a gentle scale so this
        // rarely maxes out the slider on its own.
        var tint = Math.Clamp(-greenExcess / 1.5, -100, 100);
        return tint;
    }

    private static double EstimateExposureStops(SKBitmap sample)
    {
        const double targetLuminance = 118.0; // roughly mid-gray on a 0..255 scale
        var pixels = sample.Pixels;

        double sumLuma = 0;
        foreach (var pixel in pixels)
        {
            sumLuma += 0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue;
        }

        var avgLuma = Math.Max(1, sumLuma / Math.Max(1, pixels.Length));
        var stops = Math.Log2(targetLuminance / avgLuma);

        // Keep the automatic suggestion within a sane range -- a huge swing
        // usually means the estimate is being thrown off by something like a
        // mostly-black or mostly-white frame, not that the shot is really
        // that far off.
        return Math.Clamp(stops, -2.0, 2.0);
    }
}
