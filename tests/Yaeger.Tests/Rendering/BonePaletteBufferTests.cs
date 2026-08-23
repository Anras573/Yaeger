using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side texel packing — no GL context needed, unlike the rest of Rendering/.
public class BonePaletteBufferTests
{
    [Fact]
    public void TotalTexels_SumsBoneCountsTimesFour()
    {
        var palettes = new Matrix4x4[][] { new Matrix4x4[2], new Matrix4x4[3] };

        Assert.Equal(20, BonePaletteBuffer.TotalTexels(palettes));
    }

    [Fact]
    public void TotalTexels_RespectsStartAndCount()
    {
        var palettes = new Matrix4x4[][] { new Matrix4x4[2], new Matrix4x4[3], new Matrix4x4[1] };

        Assert.Equal(4, BonePaletteBuffer.TotalTexels(palettes, start: 2, count: 1));
        Assert.Equal(20, BonePaletteBuffer.TotalTexels(palettes, start: 0, count: 2));
    }

    [Fact]
    public void Pack_WritesEachMatrixAsFourTexelsInRawFieldOrder()
    {
        // A distinctive (non-identity, non-symmetric) matrix so a transposed or reordered pack
        // would be caught: M11..M44 must land as four Vector4 rows in exactly that order, matching
        // the same raw-memory convention Renderer3D.SetBoneMatrices already uses for the UBO path.
        var matrix = new Matrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
        var palettes = new[] { new[] { matrix } };
        var destination = new Vector4[4];

        var written = BonePaletteBuffer.Pack(palettes, destination);

        Assert.Equal(4, written);
        Assert.Equal(new Vector4(1, 2, 3, 4), destination[0]);
        Assert.Equal(new Vector4(5, 6, 7, 8), destination[1]);
        Assert.Equal(new Vector4(9, 10, 11, 12), destination[2]);
        Assert.Equal(new Vector4(13, 14, 15, 16), destination[3]);
    }

    [Fact]
    public void Pack_MultiplePalettes_WritesThemBackToBackInOrder()
    {
        // Translation lands in the last row (M41-M44) of a System.Numerics Matrix4x4, i.e. the
        // fourth texel of each bone — the same raw-memory convention SetBoneMatrices already relies
        // on for the UBO path, so that's the texel these assertions distinguish the two bones by.
        var first = new[] { Matrix4x4.CreateTranslation(1f, 0f, 0f) };
        var second = new[] { Matrix4x4.CreateTranslation(2f, 0f, 0f), Matrix4x4.Identity };
        var palettes = new[] { first, second };
        var destination = new Vector4[BonePaletteBuffer.TotalTexels(palettes)];

        var written = BonePaletteBuffer.Pack(palettes, destination);

        Assert.Equal(12, written);
        // First palette's one bone occupies texels [0, 4); second palette's two bones start
        // immediately after at texel 4, regardless of the first palette's own bone count.
        Assert.Equal(new Vector4(1, 0, 0, 1), destination[3]);
        Assert.Equal(new Vector4(2, 0, 0, 1), destination[7]);
        Assert.Equal(new Vector4(0, 0, 0, 1), destination[11]);
    }

    [Fact]
    public void Pack_RespectsStartAndCount()
    {
        var palettes = new[]
        {
            new[] { Matrix4x4.CreateTranslation(1f, 0f, 0f) },
            new[] { Matrix4x4.CreateTranslation(2f, 0f, 0f) },
            new[] { Matrix4x4.CreateTranslation(3f, 0f, 0f) },
        };
        var destination = new Vector4[4];

        var written = BonePaletteBuffer.Pack(palettes, start: 1, count: 1, destination);

        Assert.Equal(4, written);
        Assert.Equal(new Vector4(2, 0, 0, 1), destination[3]);
    }
}
