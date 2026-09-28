namespace SignalHouse.Core.Imaging.RawDecoding;

/// <summary>
/// The result of decoding a camera RAW file: full-resolution, demosaiced
/// 16-bit-per-channel RGB pixel data plus the metadata needed to seed the
/// RAW editor's controls with sensible starting values (e.g. auto white
/// balance can start from the camera's own recorded white balance instead
/// of a flat 6500K guess).
/// </summary>
public sealed class RawDecodeResult
{
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>Interleaved RGB, 16 bits per channel, row-major, no padding. Length is Width * Height * 3.</summary>
    public required ushort[] Rgb16 { get; init; }

    public string CameraMake { get; init; } = string.Empty;
    public string CameraModel { get; init; } = string.Empty;
    public double IsoSpeed { get; init; }
    public double ShutterSeconds { get; init; }
    public double Aperture { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }
}
