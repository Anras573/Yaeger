using System.Numerics;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Uniform-grid spatial hash for broadphase collision culling.
/// Maps world-space AABBs to integer grid cells; items that share no cell with a queried region
/// cannot be overlapping it and are skipped before the narrowphase. Supports incremental
/// <see cref="Remove"/> (so a persistent hash can be patched as colliders move) and region
/// <see cref="Query"/>.
/// </summary>
internal sealed class SpatialHash(float cellSize, bool throwOnOversize = true)
{
    /// <summary>
    /// The most cells a single insert or query may span. Guards against degenerate
    /// configurations (e.g. a tiny cell size with large colliders) that would make the nested
    /// loops iterate millions of times in a single frame.
    /// </summary>
    internal const long MaxCellsPerInsert = 1 << 16; // 65536

    // Keyed by the two cell coordinates packed into one long: cheaper to hash and compare than
    // a value-tuple key through the default comparer.
    private readonly Dictionary<long, List<int>> _cells = new();

    // Pool of cleared List<int> objects ready for reuse, avoiding per-frame allocation.
    private readonly Stack<List<int>> _listPool = new();

    private static long Pack(int cx, int cy) => ((long)cx << 32) | (uint)cy;

    internal void Clear()
    {
        foreach (var list in _cells.Values)
        {
            list.Clear();
            _listPool.Push(list);
        }
        _cells.Clear();
    }

    /// <summary>
    /// Computes the inclusive cell range covered by an AABB. Returns <c>false</c> for a
    /// non-finite or out-of-<see cref="int"/>-range box (it can't be indexed at all).
    /// </summary>
    private bool TryGetCellRange(
        Vector2 min,
        Vector2 max,
        out int minCx,
        out int minCy,
        out int maxCx,
        out int maxCy
    )
    {
        minCx = minCy = maxCx = maxCy = 0;

        if (
            !float.IsFinite(min.X)
            || !float.IsFinite(min.Y)
            || !float.IsFinite(max.X)
            || !float.IsFinite(max.Y)
        )
            return false;

        var fMinCx = MathF.Floor(min.X / cellSize);
        var fMinCy = MathF.Floor(min.Y / cellSize);
        var fMaxCx = MathF.Floor(max.X / cellSize);
        var fMaxCy = MathF.Floor(max.Y / cellSize);

        // Division by a very small cellSize or extreme coordinates can overflow float to
        // ±Infinity, or produce values outside the int range. Skip rather than cast to a
        // corrupt index.
        if (
            !float.IsFinite(fMinCx)
            || !float.IsFinite(fMinCy)
            || !float.IsFinite(fMaxCx)
            || !float.IsFinite(fMaxCy)
            || fMinCx < int.MinValue
            || fMinCy < int.MinValue
            || fMaxCx > int.MaxValue
            || fMaxCy > int.MaxValue
        )
            return false;

        minCx = (int)fMinCx;
        minCy = (int)fMinCy;
        maxCx = (int)fMaxCx;
        maxCy = (int)fMaxCy;
        return true;
    }

    private static long CellCount(int minCx, int minCy, int maxCx, int maxCy) =>
        ((long)maxCx - minCx + 1) * ((long)maxCy - minCy + 1);

    /// <summary>
    /// Inserts <paramref name="id"/> into every cell its AABB overlaps. Returns <c>false</c>
    /// without inserting when the box can't be indexed: non-finite coordinates, or — unless
    /// the hash was built to throw — spanning more than <see cref="MaxCellsPerInsert"/> cells.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The box spans too many cells and this hash was created with <c>throwOnOversize</c>.
    /// </exception>
    internal bool Insert(int id, Vector2 min, Vector2 max)
    {
        if (!TryGetCellRange(min, max, out var minCx, out var minCy, out var maxCx, out var maxCy))
            return false;

        var cells = CellCount(minCx, minCy, maxCx, maxCy);
        if (cells > MaxCellsPerInsert)
        {
            if (!throwOnOversize)
                return false;

            throw new InvalidOperationException(
                $"Collider covers {(long)maxCx - minCx + 1}×{(long)maxCy - minCy + 1} = {cells} broadphase cells, "
                    + $"exceeding the limit of {MaxCellsPerInsert}. Increase the spatial-hash cell size."
            );
        }

        for (var cx = minCx; cx <= maxCx; cx++)
        {
            for (var cy = minCy; cy <= maxCy; cy++)
            {
                var key = Pack(cx, cy);
                if (!_cells.TryGetValue(key, out var list))
                {
                    list = _listPool.Count > 0 ? _listPool.Pop() : [];
                    _cells[key] = list;
                }
                list.Add(id);
            }
        }

        return true;
    }

    /// <summary>
    /// Removes <paramref name="id"/> from the cells covered by the AABB it was inserted with
    /// (the same <paramref name="min"/>/<paramref name="max"/> must be passed). Cells left empty
    /// are released.
    /// </summary>
    internal void Remove(int id, Vector2 min, Vector2 max)
    {
        if (!TryGetCellRange(min, max, out var minCx, out var minCy, out var maxCx, out var maxCy))
            return;
        if (CellCount(minCx, minCy, maxCx, maxCy) > MaxCellsPerInsert)
            return;

        for (var cx = minCx; cx <= maxCx; cx++)
        {
            for (var cy = minCy; cy <= maxCy; cy++)
            {
                var key = Pack(cx, cy);
                if (!_cells.TryGetValue(key, out var list))
                    continue;

                var index = list.IndexOf(id);
                if (index < 0)
                    continue;

                // Swap-with-last removal; cell order is irrelevant (callers sort what they need).
                var last = list.Count - 1;
                list[index] = list[last];
                list.RemoveAt(last);

                if (list.Count == 0)
                {
                    _cells.Remove(key);
                    _listPool.Push(list);
                }
            }
        }
    }

    /// <summary>
    /// Appends the ids stored in every cell the region overlaps to <paramref name="results"/>.
    /// An id spanning several of those cells appears once per shared cell, and ids are only
    /// cell-level candidates — callers dedupe and run their own exact overlap test. Returns
    /// <c>false</c> (appending nothing) when the region spans more than
    /// <see cref="MaxCellsPerInsert"/> cells, so the caller can fall back to a linear scan
    /// instead of walking millions of cells.
    /// </summary>
    internal bool Query(Vector2 min, Vector2 max, List<int> results)
    {
        if (!TryGetCellRange(min, max, out var minCx, out var minCy, out var maxCx, out var maxCy))
            return false;
        if (CellCount(minCx, minCy, maxCx, maxCy) > MaxCellsPerInsert)
            return false;

        for (var cx = minCx; cx <= maxCx; cx++)
        {
            for (var cy = minCy; cy <= maxCy; cy++)
            {
                if (_cells.TryGetValue(Pack(cx, cy), out var list))
                    results.AddRange(list);
            }
        }

        return true;
    }
}
