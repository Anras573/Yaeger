namespace Yaeger.Physics;

/// <summary>
/// Greedy rectangle merging over a solid/non-solid tile grid, used to collapse a tilemap's
/// solid tiles into the fewest axis-aligned rectangles for collision purposes (instead of one
/// collider per tile).
/// </summary>
public static class TilemapColliderMerger
{
    /// <summary>
    /// A merged rectangle of solid cells, expressed in grid coordinates: <see cref="Column"/>
    /// and <see cref="Row"/> are the zero-based cell of the rectangle's top-left corner,
    /// spanning <see cref="Width"/> columns and <see cref="Height"/> rows.
    /// </summary>
    public readonly record struct Rectangle(int Column, int Row, int Width, int Height);

    /// <summary>
    /// Merges adjacent solid cells in a <paramref name="width"/> × <paramref name="height"/>
    /// grid into the fewest axis-aligned rectangles, using a greedy expand-right-then-down
    /// scan. The result fully covers every solid cell exactly once (no overlaps, no gaps)
    /// but is not guaranteed to be the globally minimal rectangle count.
    /// </summary>
    /// <param name="width">Grid width in cells. Must be non-negative.</param>
    /// <param name="height">Grid height in cells. Must be non-negative.</param>
    /// <param name="isSolid">Predicate returning whether the cell at (column, row) is solid.</param>
    /// <returns>The merged rectangles, in no particular order.</returns>
    public static IReadOnlyList<Rectangle> Merge(
        int width,
        int height,
        Func<int, int, bool> isSolid
    )
    {
        ArgumentNullException.ThrowIfNull(isSolid);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        var result = new List<Rectangle>();
        if (width == 0 || height == 0)
            return result;

        var visited = new bool[width * height];

        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var index = row * width + col;
                if (visited[index] || !isSolid(col, row))
                    continue;

                // Expand right while the row continues to be solid and unclaimed.
                var w = 1;
                while (col + w < width && !visited[index + w] && isSolid(col + w, row))
                    w++;

                // Expand down while the whole [col, col+w) span below is solid and unclaimed.
                var h = 1;
                while (row + h < height)
                {
                    var rowStart = (row + h) * width + col;
                    var canExtend = true;
                    for (var k = 0; k < w; k++)
                    {
                        if (visited[rowStart + k] || !isSolid(col + k, row + h))
                        {
                            canExtend = false;
                            break;
                        }
                    }

                    if (!canExtend)
                        break;

                    h++;
                }

                for (var rr = 0; rr < h; rr++)
                {
                    var rowStart = (row + rr) * width + col;
                    for (var cc = 0; cc < w; cc++)
                        visited[rowStart + cc] = true;
                }

                result.Add(new Rectangle(col, row, w, h));
            }
        }

        return result;
    }
}
