using System.Runtime.InteropServices;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace SignalHouse.Core.Imaging.RawDecoding;

/// <summary>
/// Decodes camera RAW files via native LibRaw (for demosaiced pixel data)
/// and MetadataExtractor (for camera make/model/exposure metadata -- a
/// pure-.NET library, so metadata reading needs no native binary even
/// though pixel decoding does). See <see cref="LibRawNative"/> for the
/// native dependency this class requires you to add.
///
/// This decoder deliberately leaves LibRaw's own white-balance and color
/// pipeline at its defaults and does all white-balance / color-balance /
/// tone correction in <see cref="ColorAdjustmentEngine"/> afterward. That
/// keeps this class from needing to poke fields inside LibRaw's large,
/// version-fragile `libraw_output_params_t` struct, and it means the exact
/// same adjustment math (and the exact same saved templates) apply whether
/// the source was a RAW file or a JPEG.
/// </summary>
public sealed class LibRawDecoder : IRawDecoder
{
    public IReadOnlyList<string> SupportedExtensions { get; } = new[]
    {
        ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2", ".raf", ".pef", ".srw",
    };

    public RawDecodeResult Decode(string filePath)
    {
        var handle = LibRawNative.libraw_init(0);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "LibRaw failed to initialize (libraw_init returned null). Confirm the native " +
                "libraw shared library is present next to the executable -- see LibRawNative.cs.");
        }

        try
        {
            Check(LibRawNative.libraw_open_file(handle, filePath), "opening");
            Check(LibRawNative.libraw_unpack(handle), "unpacking");
            Check(LibRawNative.libraw_dcraw_process(handle), "processing");

            var imagePtr = LibRawNative.libraw_dcraw_make_mem_image(handle, out var errorCode);
            if (imagePtr == IntPtr.Zero)
            {
                throw new InvalidOperationException($"LibRaw failed to build the output image: {LibRawNative.GetError(errorCode)}");
            }

            try
            {
                var header = Marshal.PtrToStructure<LibRawNative.LibRawProcessedImage>(imagePtr);
                if (header.Colors != 3)
                {
                    throw new NotSupportedException(
                        $"Unexpected LibRaw output: {header.Colors} color channels (expected 3/RGB). " +
                        "This can happen for unusual monochrome-sensor RAW files.");
                }

                if (header.Bits != 8 && header.Bits != 16)
                {
                    throw new NotSupportedException($"Unexpected LibRaw output bit depth: {header.Bits}.");
                }

                var pixelCount = header.Width * header.Height * header.Colors;
                var dataPtr = imagePtr + LibRawNative.LibRawProcessedImage.DataOffset;
                var rgb16 = header.Bits == 16
                    ? ReadRgb16(dataPtr, pixelCount)
                    : ReadRgb8As16(dataPtr, pixelCount);

                var metadata = ReadMetadata(filePath);

                return new RawDecodeResult
                {
                    Width = header.Width,
                    Height = header.Height,
                    Rgb16 = rgb16,
                    CameraMake = metadata.Make,
                    CameraModel = metadata.Model,
                    IsoSpeed = metadata.Iso,
                    ShutterSeconds = metadata.Shutter,
                    Aperture = metadata.Aperture,
                    CapturedAt = metadata.CapturedAt,
                };
            }
            finally
            {
                LibRawNative.libraw_dcraw_clear_mem(imagePtr);
            }
        }
        finally
        {
            LibRawNative.libraw_close(handle);
        }
    }

    /// <summary>Copies a native 16-bit-per-channel buffer directly (little-endian, matching all desktop CPU targets).</summary>
    private static ushort[] ReadRgb16(IntPtr dataPtr, int pixelCount)
    {
        var byteCount = pixelCount * sizeof(ushort);
        var bytes = new byte[byteCount];
        Marshal.Copy(dataPtr, bytes, 0, byteCount);

        var rgb16 = new ushort[pixelCount];
        Buffer.BlockCopy(bytes, 0, rgb16, 0, byteCount);
        return rgb16;
    }

    /// <summary>Widens an 8-bit-per-channel buffer to 16-bit by shifting each byte into the high byte, preserving relative brightness.</summary>
    private static ushort[] ReadRgb8As16(IntPtr dataPtr, int pixelCount)
    {
        var bytes = new byte[pixelCount];
        Marshal.Copy(dataPtr, bytes, 0, pixelCount);

        var rgb16 = new ushort[pixelCount];
        for (var i = 0; i < pixelCount; i++)
        {
            rgb16[i] = (ushort)(bytes[i] << 8);
        }

        return rgb16;
    }

    private static void Check(int code, string stage)
    {
        if (code != 0)
        {
            throw new InvalidOperationException($"LibRaw failed while {stage} the file: {LibRawNative.GetError(code)}");
        }
    }

    private readonly record struct RawMetadata(string Make, string Model, double Iso, double Shutter, double Aperture, DateTimeOffset? CapturedAt);

    private static RawMetadata ReadMetadata(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            var make = exifIfd0?.GetDescription(ExifIfd0Directory.TagMake) ?? string.Empty;
            var model = exifIfd0?.GetDescription(ExifIfd0Directory.TagModel) ?? string.Empty;

            var iso = 0.0;
            exifSubIfd?.TryGetDouble(ExifSubIfdDirectory.TagIsoEquivalent, out iso);

            var aperture = 0.0;
            exifSubIfd?.TryGetDouble(ExifSubIfdDirectory.TagFNumber, out aperture);

            var shutter = 0.0;
            exifSubIfd?.TryGetDouble(ExifSubIfdDirectory.TagExposureTime, out shutter);

            DateTimeOffset? capturedAt = null;
            if (exifSubIfd is not null && exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out var dateTime))
            {
                capturedAt = new DateTimeOffset(dateTime);
            }

            return new RawMetadata(make.Trim(), model.Trim(), iso, shutter, aperture, capturedAt);
        }
        catch
        {
            // Metadata is a convenience, not a requirement for the decode to
            // succeed -- fall back to defaults rather than failing the whole
            // file over an unreadable or missing EXIF block.
            return new RawMetadata(string.Empty, string.Empty, 0, 0, 0, null);
        }
    }
}
