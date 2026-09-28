namespace SignalHouse.Core.Models;

/// <summary>
/// A named passport/visa photo size specification. Millimeters are the
/// source of truth; pixel dimensions are derived from the export DPI (see
/// <see cref="ComputePixelSize"/>). Head-height bounds are the fraction of
/// the frame the subject's head (chin to crown) should occupy, per the
/// relevant government's photo requirements, and are surfaced in the
/// Passport editor as a compliance guide overlay.
/// </summary>
public sealed record PassportSizePreset(
    string Name,
    double WidthMm,
    double HeightMm,
    double HeadHeightMinMm,
    double HeadHeightMaxMm)
{
    public static readonly PassportSizePreset UnitedStates = new("US Passport/Visa (2 x 2 in)", 50.8, 50.8, 25.4, 35.0);
    public static readonly PassportSizePreset Schengen = new("Schengen / EU Visa (35 x 45 mm)", 35, 45, 32, 36);
    public static readonly PassportSizePreset UnitedKingdom = new("UK Passport (35 x 45 mm)", 35, 45, 29, 34);
    public static readonly PassportSizePreset India = new("India Passport (51 x 51 mm)", 51, 51, 25, 35);
    public static readonly PassportSizePreset Canada = new("Canada Passport (50 x 70 mm)", 50, 70, 31, 36);
    public static readonly PassportSizePreset Jamaica = new("Jamaica Passport (50 x 50 mm)", 50, 50, 30, 35);
    public static readonly PassportSizePreset Australia = new("Australia Passport (35 x 45 mm)", 35, 45, 32, 36);

    public static IReadOnlyList<PassportSizePreset> All { get; } = new[]
    {
        UnitedStates, Schengen, UnitedKingdom, India, Canada, Jamaica, Australia,
    };

    /// <summary>Target output size in pixels at the given export resolution.</summary>
    public (int WidthPx, int HeightPx) ComputePixelSize(double dpi) =>
        ((int)Math.Round(WidthMm / 25.4 * dpi), (int)Math.Round(HeightMm / 25.4 * dpi));

    /// <summary>Allowed head-height range in pixels at the given export resolution, for the compliance overlay.</summary>
    public (int MinPx, int MaxPx) ComputeHeadHeightRangePx(double dpi) =>
        ((int)Math.Round(HeadHeightMinMm / 25.4 * dpi), (int)Math.Round(HeadHeightMaxMm / 25.4 * dpi));
}
