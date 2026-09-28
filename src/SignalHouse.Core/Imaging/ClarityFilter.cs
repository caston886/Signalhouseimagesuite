using SkiaSharp;

namespace SignalHouse.Core.Imaging;

/// <summary>
/// Implements "Clarity" (local contrast enhancement) as an unsharp-mask
/// style operation built entirely out of Skia's image filter graph: blur the
/// image to get its low-frequency ("what this area looks like on average")
/// version, then push each pixel away from that blurred version. Building
/// this as an SKImageFilter graph -- rather than a manual per-pixel loop --
/// lets Skia evaluate it on the GPU where available, which is what keeps it
/// fast enough to run live while the Clarity slider is being dragged.
/// </summary>
public static class ClarityFilter
{
    /// <summary>Local-contrast neighborhood radius, in pixels, at the preview resolution ImageDocument renders at.</summary>
    private const float BlurSigma = 12f;

    public static SKImageFilter Apply(SKImageFilter source, double clarity)
    {
        var amount = (float)(Math.Clamp(clarity, -100, 100) / 100.0);

        var blurred = SKImageFilter.CreateBlur(BlurSigma, BlurSigma, source);

        // result = source * (1 + amount) - blurred * amount
        //
        // SKImageFilter.CreateArithmetic computes, per pixel:
        //   k1 * foreground * background + k2 * foreground + k3 * background + k4
        // so with k1 = k4 = 0, k2 = 1 + amount, k3 = -amount, foreground =
        // source, background = blurred, this is exactly the unsharp-mask
        // formula above.
        //
        // NOTE: this call uses named arguments deliberately -- if your
        // installed SkiaSharp version names these parameters slightly
        // differently (e.g. "crop" vs "cropRect"), the compiler error will
        // point straight at the mismatched name here; everything else about
        // the math above stays correct regardless.
        return SKImageFilter.CreateArithmetic(
            k1: 0f,
            k2: 1f + amount,
            k3: -amount,
            k4: 0f,
            enforcePMColor: true,
            background: blurred,
            foreground: source,
            crop: null);
    }
}
