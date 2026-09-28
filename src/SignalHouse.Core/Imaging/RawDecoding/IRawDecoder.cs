namespace SignalHouse.Core.Imaging.RawDecoding;

public interface IRawDecoder
{
    /// <summary>
    /// Decodes a camera RAW file (CR2, NEF, ARW, DNG, and whatever else the
    /// underlying decoder supports) into full-resolution, demosaiced RGB16
    /// pixel data.
    /// </summary>
    RawDecodeResult Decode(string filePath);

    /// <summary>File extensions this decoder claims to support, lowercase, with the leading dot.</summary>
    IReadOnlyList<string> SupportedExtensions { get; }
}
