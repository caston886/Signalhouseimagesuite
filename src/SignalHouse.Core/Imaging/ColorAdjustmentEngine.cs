using SkiaSharp;
using SignalHouse.Core.Models;
using SignalHouse.Core.Utilities;

namespace SignalHouse.Core.Imaging;

/// <summary>
/// Builds a single composed SKColorFilter from an EditState's tone, white
/// balance, and color balance controls, plus (when Clarity is non-zero) an
/// SKImageFilter that layers a local-contrast pass on top. Collapsing every
/// slider into one matrix means applying the full set of adjustments to the
/// preview is a single Skia draw call per frame, which is what keeps slider
/// dragging smooth at the display's refresh rate on modest hardware.
///
/// All matrix math operates in Skia's native 0..1 normalized float color
/// space (not 0..255), matching what SKColorFilter.CreateColorMatrix expects.
/// </summary>
public static class ColorAdjustmentEngine
{
    /// <summary>Builds the combined tone/white-balance/color-balance matrix as a single SKColorFilter.</summary>
    public static SKColorFilter BuildColorFilter(EditState state)
    {
        var matrix = ColorMatrixMath.Identity;

        // Order matters: white balance and color balance operate on the
        // "raw" image first, then exposure sets overall brightness, then
        // contrast and saturation are applied last so they act on the
        // already color-corrected, exposed image -- matching the order a
        // photographer would reason about these controls in.
        matrix = ColorMatrixMath.Multiply(WhiteBalanceMatrix(state.WhiteBalanceTemperature, state.WhiteBalanceTint), matrix);
        matrix = ColorMatrixMath.Multiply(ColorBalanceMatrix(state.CyanRed, state.MagentaGreen, state.YellowBlue), matrix);
        matrix = ColorMatrixMath.Multiply(ExposureMatrix(state.Exposure), matrix);
        matrix = ColorMatrixMath.Multiply(ContrastMatrix(state.Contrast), matrix);
        matrix = ColorMatrixMath.Multiply(SaturationMatrix(state.Saturation), matrix);

        return SKColorFilter.CreateColorMatrix(matrix);
    }

    /// <summary>
    /// Builds the full preview/export filter, including the Clarity (local
    /// contrast) pass, as a single SKImageFilter chain suitable for
    /// SKPaint.ImageFilter.
    /// </summary>
    public static SKImageFilter BuildImageFilter(EditState state)
    {
        var colorFilter = BuildColorFilter(state);
        SKImageFilter filter = SKImageFilter.CreateColorFilter(colorFilter);

        if (Math.Abs(state.Clarity) > 0.01)
        {
            filter = ClarityFilter.Apply(filter, state.Clarity);
        }

        return filter;
    }

    private static float[] ExposureMatrix(double stops)
    {
        var gain = (float)Math.Pow(2, stops);
        return new[]
        {
            gain, 0, 0, 0, 0,
            0, gain, 0, 0, 0,
            0, 0, gain, 0, 0,
            0, 0, 0, 1, 0,
        };
    }

    private static float[] ContrastMatrix(double contrast)
    {
        var c = (float)(1.0 + Math.Clamp(contrast, -100, 100) / 100.0);
        var pivotOffset = (1 - c) * 0.5f; // pivot around mid-gray (0.5 in 0..1 space)

        return new[]
        {
            c, 0, 0, 0, pivotOffset,
            0, c, 0, 0, pivotOffset,
            0, 0, c, 0, pivotOffset,
            0, 0, 0, 1, 0,
        };
    }

    private static float[] SaturationMatrix(double saturation)
    {
        var s = (float)(1.0 + Math.Clamp(saturation, -100, 100) / 100.0);

        // Rec. 709 luma coefficients, so saturation changes preserve
        // perceived brightness instead of muddying or blowing out the image.
        const float lr = 0.2126f;
        const float lg = 0.7152f;
        const float lb = 0.0722f;
        var sr = (1 - s) * lr;
        var sg = (1 - s) * lg;
        var sb = (1 - s) * lb;

        return new[]
        {
            sr + s, sg, sb, 0, 0,
            sr, sg + s, sb, 0, 0,
            sr, sg, sb + s, 0, 0,
            0, 0, 0, 1, 0,
        };
    }

    private static float[] WhiteBalanceMatrix(double kelvin, double tint)
    {
        var (r, g, b) = KelvinToRgbGain(kelvin);

        // Tint moves along the green/magenta axis, independent of temperature.
        var tintNorm = (float)(Math.Clamp(tint, -100, 100) / 100.0);
        g *= 1 - tintNorm * 0.3f;
        if (tintNorm > 0)
        {
            r *= 1 + tintNorm * 0.15f;
            b *= 1 + tintNorm * 0.15f;
        }

        return new[]
        {
            r, 0, 0, 0, 0,
            0, g, 0, 0, 0,
            0, 0, b, 0, 0,
            0, 0, 0, 1, 0,
        };
    }

    private static float[] ColorBalanceMatrix(double cyanRed, double magentaGreen, double yellowBlue)
    {
        const float scale = 0.5f; // slider units (-100..100) mapped to a 0..1-space offset range
        var cr = (float)(Math.Clamp(cyanRed, -100, 100) / 100.0) * scale;
        var mg = (float)(Math.Clamp(magentaGreen, -100, 100) / 100.0) * scale;
        var yb = (float)(Math.Clamp(yellowBlue, -100, 100) / 100.0) * scale;

        // Each slider pushes its own channel and pulls the complementary
        // channels back slightly, matching how a photographic color-balance
        // wheel behaves (pushing toward red pulls away from cyan, which is
        // itself a blend of green and blue).
        var rOffset = cr - yb / 2 - mg / 2;
        var gOffset = mg - yb / 2 - cr / 2;
        var bOffset = yb - cr / 2 - mg / 2;

        return new[]
        {
            1, 0, 0, 0, rOffset,
            0, 1, 0, 0, gOffset,
            0, 0, 1, 0, bOffset,
            0, 0, 0, 1, 0,
        };
    }

    /// <summary>
    /// Converts a Kelvin temperature into per-channel gain, normalized
    /// against 6500K (daylight, the app's default) so the white balance
    /// slider is a no-op at its default value and moves smoothly warmer or
    /// cooler from there. The underlying black-body curve is shared with
    /// AutoAdjustmentEstimator -- see <see cref="BlackBodyRadiator"/>.
    /// </summary>
    private static (float r, float g, float b) KelvinToRgbGain(double kelvin)
    {
        var (r, g, b) = BlackBodyRadiator.ToRgb255(Math.Clamp(kelvin, 1000, 40000));
        var (rRef, gRef, bRef) = BlackBodyRadiator.ToRgb255(6500);
        return ((float)(rRef / r), (float)(gRef / g), (float)(bRef / b));
    }
}
