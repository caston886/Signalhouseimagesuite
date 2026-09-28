namespace SignalHouse.Core.Utilities;

/// <summary>
/// Helper for composing Skia-style color matrices: 20 floats representing a
/// 4x5 row-major matrix (4 output rows for R,G,B,A; 5 columns for the R,G,B,A
/// input channels plus a constant offset column). <see cref="Multiply"/>
/// composes two such matrices into the single matrix that has the same
/// effect as applying <paramref name="b"/>'s transform first and then
/// <paramref name="a"/>'s -- which lets ColorAdjustmentEngine fold exposure,
/// contrast, saturation, white balance, and color balance into one matrix
/// and hand Skia a single SKColorFilter, instead of chaining several filters
/// and paying for multiple passes on every frame.
/// </summary>
public static class ColorMatrixMath
{
    public static readonly float[] Identity =
    {
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 1, 0,
    };

    /// <summary>Returns the matrix equivalent to applying <paramref name="b"/> then <paramref name="a"/>.</summary>
    public static float[] Multiply(float[] a, float[] b)
    {
        // Treat both as 5x5 affine matrices with an implicit trailing
        // [0, 0, 0, 0, 1] row, multiply as ordinary 5x5 matrices, then take
        // the top 4 rows back out.
        Span<float> fa = stackalloc float[25];
        Span<float> fb = stackalloc float[25];
        Span<float> product = stackalloc float[25];
        ToFull(a, fa);
        ToFull(b, fb);

        for (var row = 0; row < 5; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                float sum = 0;
                for (var k = 0; k < 5; k++)
                {
                    sum += fa[row * 5 + k] * fb[k * 5 + col];
                }

                product[row * 5 + col] = sum;
            }
        }

        var result = new float[20];
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                result[row * 5 + col] = product[row * 5 + col];
            }
        }

        return result;
    }

    private static void ToFull(float[] m, Span<float> full)
    {
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                full[row * 5 + col] = m[row * 5 + col];
            }
        }

        full[20] = 0;
        full[21] = 0;
        full[22] = 0;
        full[23] = 0;
        full[24] = 1;
    }
}
