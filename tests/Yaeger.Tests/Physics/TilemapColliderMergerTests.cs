using Yaeger.Physics;

namespace Yaeger.Tests.Physics;

public class TilemapColliderMergerTests
{
    private static Func<int, int, bool> FromGrid(int width, bool[] solid) =>
        (column, row) => solid[row * width + column];

    [Fact]
    public void Merge_EmptyGrid_ShouldReturnNoRectangles()
    {
        var result = TilemapColliderMerger.Merge(0, 0, (_, _) => false);

        Assert.Empty(result);
    }

    [Fact]
    public void Merge_NoSolidTiles_ShouldReturnNoRectangles()
    {
        var solid = new bool[9];
        var result = TilemapColliderMerger.Merge(3, 3, FromGrid(3, solid));

        Assert.Empty(result);
    }

    [Fact]
    public void Merge_SingleTile_ShouldReturnOneUnitRectangle()
    {
        var solid = new[] { false, false, false, false, true, false, false, false, false };
        var result = TilemapColliderMerger.Merge(3, 3, FromGrid(3, solid));

        var rect = Assert.Single(result);
        Assert.Equal(new TilemapColliderMerger.Rectangle(1, 1, 1, 1), rect);
    }

    [Fact]
    public void Merge_HorizontalRow_ShouldMergeIntoOneRectangle()
    {
        // A single row of 5 adjacent solid tiles should merge into one wide rectangle,
        // not five unit colliders (the "flat floor" acceptance criterion).
        var solid = new[] { true, true, true, true, true };
        var result = TilemapColliderMerger.Merge(5, 1, FromGrid(5, solid));

        var rect = Assert.Single(result);
        Assert.Equal(new TilemapColliderMerger.Rectangle(0, 0, 5, 1), rect);
    }

    [Fact]
    public void Merge_VerticalColumn_ShouldMergeIntoOneRectangle()
    {
        var solid = new[] { true, true, true, true };
        var result = TilemapColliderMerger.Merge(1, 4, FromGrid(1, solid));

        var rect = Assert.Single(result);
        Assert.Equal(new TilemapColliderMerger.Rectangle(0, 0, 1, 4), rect);
    }

    [Fact]
    public void Merge_SolidBlock_ShouldMergeIntoOneRectangle()
    {
        // 4x3 grid, fully solid.
        var solid = Enumerable.Repeat(true, 12).ToArray();
        var result = TilemapColliderMerger.Merge(4, 3, FromGrid(4, solid));

        var rect = Assert.Single(result);
        Assert.Equal(new TilemapColliderMerger.Rectangle(0, 0, 4, 3), rect);
    }

    [Fact]
    public void Merge_LShape_ShouldCoverEverySolidCellExactlyOnce()
    {
        // 3x3 grid, L-shape:
        // X . .
        // X . .
        // X X X
        var solid = new[] { true, false, false, true, false, false, true, true, true };
        var isSolid = FromGrid(3, solid);

        var result = TilemapColliderMerger.Merge(3, 3, isSolid);

        AssertExactCoverage(3, 3, isSolid, result);
    }

    [Fact]
    public void Merge_GridWithHoles_ShouldNotCoverHoles()
    {
        // 3x3 grid with a hole in the middle:
        // X X X
        // X . X
        // X X X
        var solid = new[] { true, true, true, true, false, true, true, true, true };
        var isSolid = FromGrid(3, solid);

        var result = TilemapColliderMerger.Merge(3, 3, isSolid);

        AssertExactCoverage(3, 3, isSolid, result);
    }

    [Fact]
    public void Merge_DisjointRegions_ShouldProduceSeparateRectangles()
    {
        // 5x1 grid: two separate solid runs separated by a gap.
        var solid = new[] { true, true, false, true, true };
        var result = TilemapColliderMerger.Merge(5, 1, FromGrid(5, solid));

        Assert.Equal(2, result.Count);
        Assert.Contains(new TilemapColliderMerger.Rectangle(0, 0, 2, 1), result);
        Assert.Contains(new TilemapColliderMerger.Rectangle(3, 0, 2, 1), result);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void Merge_NegativeDimensions_ShouldThrow(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TilemapColliderMerger.Merge(width, height, (_, _) => false)
        );
    }

    [Fact]
    public void Merge_NullPredicate_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => TilemapColliderMerger.Merge(1, 1, null!));
    }

    /// <summary>
    /// Verifies the merged rectangles are non-overlapping and their union is exactly the set
    /// of solid cells — no cell is covered twice, and no non-solid cell is covered at all.
    /// </summary>
    private static void AssertExactCoverage(
        int width,
        int height,
        Func<int, int, bool> isSolid,
        IReadOnlyList<TilemapColliderMerger.Rectangle> rectangles
    )
    {
        var coverCount = new int[width * height];
        foreach (var rect in rectangles)
        {
            for (var row = rect.Row; row < rect.Row + rect.Height; row++)
            {
                for (var col = rect.Column; col < rect.Column + rect.Width; col++)
                {
                    coverCount[row * width + col]++;
                }
            }
        }

        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var count = coverCount[row * width + col];
                if (isSolid(col, row))
                    Assert.Equal(1, count);
                else
                    Assert.Equal(0, count);
            }
        }
    }
}
