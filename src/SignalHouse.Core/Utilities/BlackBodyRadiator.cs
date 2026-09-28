namespace SignalHouse.Core.Utilities;

/// <summary>
/// Approximates a black-body radiator's RGB color at a given Kelvin
/// temperature -- the widely used Tanner Helland piecewise-polynomial fit to
/// Mitchell Charity's black-body data. Shared by
/// <see cref="Imaging.ColorAdjustmentEngine"/> (to turn the white balance
/// slider into a color matrix) and
/// <see cref="Imaging.AutoAdjustmentEstimator"/> (to search for the Kelvin
/// value that best explains an image's observed color cast, for the RAW
/// editor's Auto white balance).
/// </summary>
public static class BlackBodyRadiator
{
    /// <summary>Returns the black-body color at the given Kelvin temperature, each channel in 1..255 (never 0, so the result is safe to use as a divisor).</summary>
    public static (double R, double G, double B) ToRgb255(double kelvin)
    {
        var temp = kelvin / 100.0;

        double r = temp <= 66
            ? 255
            : 329.698727446 * Math.Pow(temp - 60, -0.1332047592);

        double g = temp <= 66
            ? 99.4708025861 * Math.Log(temp) - 161.1195681661
            : 288.1221695283 * Math.Pow(temp - 60, -0.0755148492);

        double b = temp >= 66
            ? 255
            : temp <= 19
                ? 0
                : 138.5177312231 * Math.Log(temp - 10) - 305.0447927307;

        return (Math.Clamp(r, 1, 255), Math.Clamp(g, 1, 255), Math.Clamp(b, 1, 255));
    }
}
