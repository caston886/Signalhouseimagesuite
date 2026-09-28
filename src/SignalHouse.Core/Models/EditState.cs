namespace SignalHouse.Core.Models;

/// <summary>
/// A complete, serializable snapshot of every adjustment that can be applied
/// to an image in any of the three editors. This is the single shared
/// "recipe" format used by the Passport, JPEG, and RAW editors alike, which
/// is what makes a template created in one editor immediately usable in
/// either of the other two.
/// </summary>
public sealed class EditState
{
    // --- Basic tone -----------------------------------------------------
    /// <summary>Exposure in stops. Typical usable range -5..+5.</summary>
    public double Exposure { get; set; }

    /// <summary>-100..100.</summary>
    public double Contrast { get; set; }

    /// <summary>-100..100.</summary>
    public double Saturation { get; set; }

    // --- White balance ----------------------------------------------------
    /// <summary>Kelvin, 2000..12000. 6500 (daylight) is a no-op.</summary>
    public double WhiteBalanceTemperature { get; set; } = 6500;

    /// <summary>-100 (green) .. +100 (magenta).</summary>
    public double WhiteBalanceTint { get; set; }

    // --- Color balance (global lift toward each pole) ----------------------
    /// <summary>-100 (cyan) .. +100 (red).</summary>
    public double CyanRed { get; set; }

    /// <summary>-100 (magenta) .. +100 (green).</summary>
    public double MagentaGreen { get; set; }

    /// <summary>-100 (yellow) .. +100 (blue).</summary>
    public double YellowBlue { get; set; }

    // --- Detail -------------------------------------------------------------
    /// <summary>Local contrast enhancement. -100..100.</summary>
    public double Clarity { get; set; }

    // --- Geometry (kept here too so a preset can restore crop/rotate) -------
    public double RotationDegrees { get; set; }

    public EditState Clone() => (EditState)MemberwiseClone();

    public static EditState Default() => new();

    /// <summary>True if every value is at its neutral/default setting.</summary>
    public bool IsNeutral()
    {
        var d = Default();
        return Exposure.Equals(d.Exposure)
            && Contrast.Equals(d.Contrast)
            && Saturation.Equals(d.Saturation)
            && WhiteBalanceTemperature.Equals(d.WhiteBalanceTemperature)
            && WhiteBalanceTint.Equals(d.WhiteBalanceTint)
            && CyanRed.Equals(d.CyanRed)
            && MagentaGreen.Equals(d.MagentaGreen)
            && YellowBlue.Equals(d.YellowBlue)
            && Clarity.Equals(d.Clarity)
            && RotationDegrees.Equals(d.RotationDegrees);
    }
}
