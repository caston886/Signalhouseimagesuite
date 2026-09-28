using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace SignalHouse.Core.Imaging;

/// <summary>
/// Automatic face detection, used to auto-center a passport crop on the
/// subject's face and to validate head-height compliance against a
/// <see cref="Models.PassportSizePreset"/>.
///
/// *** REQUIRES A MODEL FILE THIS PROJECT DOES NOT INCLUDE ***
/// Add a lightweight face-detector ONNX model, such as
/// Ultra-Light-Fast-Generic-Face-Detector-1MB or BlazeFace. This workspace
/// has no network access to fetch or verify one, so confirm the exact input/
/// output shapes on your machine and adjust the constants and parsing below
/// if they differ -- the values here (320x240 input; two outputs, "scores"
/// shaped [1,N,2] and "boxes" shaped [1,N,4]) match
/// Ultra-Light-Fast-Generic-Face-Detector's published ONNX export.
///
/// Until a model is wired in, the Passport editor's manual crop tool (see
/// PassportEditorViewModel) is fully functional on its own -- this class
/// only adds the "auto-detect and auto-center" convenience on top of it.
/// </summary>
public sealed class OnnxFaceDetector : IFaceDetector, IDisposable
{
    private readonly InferenceSession _session;
    private const string InputName = "input";
    private const int InputWidth = 320;
    private const int InputHeight = 240;
    private const float ConfidenceThreshold = 0.7f;

    public OnnxFaceDetector(string modelPath)
    {
        _session = new InferenceSession(modelPath);
    }

    public IReadOnlyList<DetectedFace> Detect(SKBitmap image)
    {
        using var resized = image.Resize(new SKImageInfo(InputWidth, InputHeight), SKFilterQuality.High);
        var input = new DenseTensor<float>(new[] { 1, 3, InputHeight, InputWidth });
        var pixels = resized.Pixels;

        for (var y = 0; y < InputHeight; y++)
        {
            for (var x = 0; x < InputWidth; x++)
            {
                var p = pixels[y * InputWidth + x];
                input[0, 0, y, x] = (p.Red - 127f) / 128f;
                input[0, 1, y, x] = (p.Green - 127f) / 128f;
                input[0, 2, y, x] = (p.Blue - 127f) / 128f;
            }
        }

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(InputName, input) });
        var resultList = results.ToList();
        var scores = resultList[0].AsTensor<float>();
        var boxes = resultList[1].AsTensor<float>();

        var faces = new List<DetectedFace>();
        var count = scores.Dimensions[1];
        var scaleX = (float)image.Width / InputWidth;
        var scaleY = (float)image.Height / InputHeight;

        for (var i = 0; i < count; i++)
        {
            var confidence = scores[0, i, 1];
            if (confidence < ConfidenceThreshold)
            {
                continue;
            }

            var left = boxes[0, i, 0] * InputWidth * scaleX;
            var top = boxes[0, i, 1] * InputHeight * scaleY;
            var right = boxes[0, i, 2] * InputWidth * scaleX;
            var bottom = boxes[0, i, 3] * InputHeight * scaleY;

            faces.Add(new DetectedFace(new SKRect(left, top, right, bottom), confidence));
        }

        return faces;
    }

    public void Dispose() => _session.Dispose();
}
