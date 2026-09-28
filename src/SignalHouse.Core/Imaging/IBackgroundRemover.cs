using SkiaSharp;

namespace SignalHouse.Core.Imaging;

public interface IBackgroundRemover
{
    /// <summary>
    /// Returns an 8-bit grayscale alpha mask, the same dimensions as the
    /// source image, where 0 means "background" and 255 means "subject".
    /// </summary>
    SKBitmap ComputeMask(SKBitmap source);
}
