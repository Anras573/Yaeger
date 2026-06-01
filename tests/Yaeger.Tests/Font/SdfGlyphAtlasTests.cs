using Yaeger.Font;

namespace Yaeger.Tests.Font;

public class SdfGlyphAtlasTests
{
    // ── ThresholdAlpha ─────────────────────────────────────────────────────────

    [Fact]
    public void ThresholdAlpha_AboveThreshold_IsMarkedInside()
    {
        byte[] alpha = [0, 128, 255];
        var mask = SdfGlyphAtlas.ThresholdAlpha(alpha, 3, 1);
        Assert.False(mask[0]);
        Assert.True(mask[1]);
        Assert.True(mask[2]);
    }

    [Fact]
    public void ThresholdAlpha_AtThreshold_IsMarkedOutside()
    {
        byte[] alpha = [127];
        var mask = SdfGlyphAtlas.ThresholdAlpha(alpha, 1, 1);
        Assert.False(mask[0]);
    }

    [Fact]
    public void ThresholdAlpha_WithRowStride_SkipsStridePadding()
    {
        // 2-pixel-wide image with 4-byte row stride (2 real + 2 padding)
        byte[] alpha = [200, 50, 0xFF, 0xFF, 200, 50, 0xFF, 0xFF];
        var mask = SdfGlyphAtlas.ThresholdAlpha(alpha, 2, 2, rowStride: 4);
        Assert.True(mask[0]);  // (0,0) = 200
        Assert.False(mask[1]); // (1,0) = 50
        Assert.True(mask[2]);  // (0,1) = 200
        Assert.False(mask[3]); // (1,1) = 50
    }

    // ── GenerateSdf ────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateSdf_InsidePixels_HaveValueAboveHalf()
    {
        // 3×3 grid: all inside
        var mask = new bool[9];
        Array.Fill(mask, true);
        var sdf = SdfGlyphAtlas.GenerateSdf(mask, 3, 3, spread: 4);
        foreach (var v in sdf)
            Assert.True(v > 0.5f, $"Expected inside pixel > 0.5 but got {v}");
    }

    [Fact]
    public void GenerateSdf_OutsidePixels_HaveValueBelowHalf()
    {
        // 3×3 grid: all outside
        var mask = new bool[9];
        Array.Fill(mask, false);
        var sdf = SdfGlyphAtlas.GenerateSdf(mask, 3, 3, spread: 4);
        foreach (var v in sdf)
            Assert.True(v < 0.5f, $"Expected outside pixel < 0.5 but got {v}");
    }

    [Fact]
    public void GenerateSdf_EdgePixel_IsNearHalf()
    {
        // A 3×1 row: [outside, boundary-inside, inside].
        // The middle pixel is adjacent to an outside pixel, so its signed
        // distance to the nearest outside pixel is 1 unit → should be
        // 0.5 + 0.5 * (1 / spread).  With spread=4 that's 0.625.
        var mask = new bool[] { false, true, true };
        var sdf = SdfGlyphAtlas.GenerateSdf(mask, 3, 1, spread: 4);

        // The outside pixel (index 0) is adjacent to an inside pixel → dist = 1 → value < 0.5
        Assert.True(sdf[0] < 0.5f);
        // The first inside pixel (index 1) is adjacent to an outside pixel → dist = 1 → value > 0.5
        Assert.True(sdf[1] > 0.5f);
        // The second inside pixel (index 2) is two steps from outside → dist = 2 → larger
        Assert.True(sdf[2] > sdf[1]);
    }

    [Fact]
    public void GenerateSdf_OutputIsClampedTo01()
    {
        var mask = new bool[16];
        Array.Fill(mask, true);
        var sdf = SdfGlyphAtlas.GenerateSdf(mask, 4, 4, spread: 1);
        foreach (var v in sdf)
        {
            Assert.True(v >= 0f && v <= 1f, $"SDF value {v} is out of [0,1]");
        }
    }

    // ── DownsampleSdf ──────────────────────────────────────────────────────────

    [Fact]
    public void DownsampleSdf_2xScale_AveragesQuadrant()
    {
        // 2×2 high-res → 1×1 output at scale 2
        // All pixels are 1.0 → output should be 255
        float[] highRes = [1f, 1f, 1f, 1f];
        var result = SdfGlyphAtlas.DownsampleSdf(highRes, 2, 2, 1, 1, 2);
        Assert.Single(result);
        Assert.Equal(255, result[0]);
    }

    [Fact]
    public void DownsampleSdf_ZeroInput_ProducesZeroOutput()
    {
        float[] highRes = new float[16]; // all zeros
        var result = SdfGlyphAtlas.DownsampleSdf(highRes, 4, 4, 2, 2, 2);
        Assert.All(result, b => Assert.Equal(0, b));
    }

    [Fact]
    public void DownsampleSdf_OutputSizeMatchesExpected()
    {
        float[] highRes = new float[4 * 4];
        var result = SdfGlyphAtlas.DownsampleSdf(highRes, 4, 4, 2, 2, 2);
        Assert.Equal(4, result.Length); // 2×2 output
    }

    // ── Integration: full SDF pipeline ────────────────────────────────────────

    [Fact]
    public void SdfPipeline_SolidSquare_CenterIsInside_CornersAreOutside()
    {
        // Build a 6×6 binary mask with a 4×4 solid square in the centre.
        int w = 8, h = 8;
        var mask = new bool[w * h];
        for (int y = 2; y < 6; y++)
        {
            for (int x = 2; x < 6; x++)
            {
                mask[y * w + x] = true;
            }
        }

        var sdf = SdfGlyphAtlas.GenerateSdf(mask, w, h, spread: 3);

        // Centre of the square should be solidly inside (> 0.5)
        float centre = sdf[4 * w + 4];
        Assert.True(centre > 0.5f, $"Centre should be > 0.5, got {centre}");

        // Corners of the image are outside
        Assert.True(sdf[0] < 0.5f, "Top-left corner should be outside");
        Assert.True(sdf[w - 1] < 0.5f, "Top-right corner should be outside");
        Assert.True(sdf[(h - 1) * w] < 0.5f, "Bottom-left corner should be outside");
        Assert.True(sdf[h * w - 1] < 0.5f, "Bottom-right corner should be outside");
    }
}
