using SkiaSharp;

namespace SignalHouse.Core.Imaging;

public sealed record DetectedFace(SKRect BoundingBox, float Confidence);

public interface IFaceDetector
{
    IReadOnlyList<DetectedFace> Detect(SKBitmap image);
}
