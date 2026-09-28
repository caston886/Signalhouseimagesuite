using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace SignalHouse.Core.Imaging;

/// <summary>
/// Automatic subject/background segmentation for arbitrary (non-studio)
/// backgrounds, using a portrait-matting ONNX model such as MODNet or
/// U2Net-portrait.
///
/// *** REQUIRES A MODEL FILE THIS PROJECT DOES NOT INCLUDE ***
/// Download a pre-trained portrait matting model in ONNX format (both
/// MODNet and U2Net publish ONNX exports) and point <paramref name="modelPath"/>
/// at it. This workspace has no network access, so that download, and
/// confirming the exact input tensor name/size the model you choose expects,
/// has to happen on your machine -- adjust <see cref="InputName"/> and
/// <see cref="InputSize"/> below to match if they differ from the values
/// here (512x512, input named "input", which matches MODNet's published
/// ONNX export).
///
/// For the common studio-backdrop passport photo case, prefer
/// <see cref="ChromaKeyBackgroundRemover"/>, which needs no model file and
/// is wired up as the Passport editor's default.
/// </summary>
public sealed class OnnxBackgroundRemover : IBackgroundRemover, IDisposable
{
    private readonly InferenceSession _session;
    private const string InputName = "input";
    private const int InputSize = 512;

    public OnnxBackgroundRemover(string modelPath)
    {
        _session = new InferenceSession(modelPath);
    }

    public SKBitmap ComputeMask(SKBitmap source)
    {
        using var resized = source.Resize(new SKImageInfo(InputSize, InputSize), SKFilterQuality.High);
        var input = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });
        var pixels = resized.Pixels;

        for (var y = 0; y < InputSize; y++)
        {
            for (var x = 0; x < InputSize; x++)
            {
                var p = pixels[y * InputSize + x];
                input[0, 0, y, x] = p.Red / 255f;
                input[0, 1, y, x] = p.Green / 255f;
                input[0, 2, y, x] = p.Blue / 255f;
            }
        }

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(InputName, input) });
        var outputTensor = results.First().AsTensor<float>();

        var maskSmall = new SKBitmap(InputSize, InputSize, SKColorType.Gray8, SKAlphaType.Opaque);
        var maskPixels = new SKColor[InputSize * InputSize];
        for (var y = 0; y < InputSize; y++)
        {
            for (var x = 0; x < InputSize; x++)
            {
                var v = (byte)Math.Clamp(outputTensor[0, 0, y, x] * 255f, 0, 255);
                maskPixels[y * InputSize + x] = new SKColor(v, v, v);
            }
        }

        maskSmall.Pixels = maskPixels;
        return maskSmall.Resize(new SKImageInfo(source.Width, source.Height), SKFilterQuality.High);
    }

    public void Dispose() => _session.Dispose();
}
