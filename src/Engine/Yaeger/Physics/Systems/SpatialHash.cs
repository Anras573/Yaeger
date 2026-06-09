using System.Numerics;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Uniform-grid spatial hash for broadphase collision culling.
/// Maps world-space AABBs to integer grid cells; pairs that share no cell
/// cannot be overlapping and are skipped before the narrowphase.
/// </summary>
internal sealed class SpatialHash(float cellSize)
{
    private readonly Dictionary<(int X, int Y), List<int>> _cells = new();
    private readonly List<(int X, int Y)> _activeCells = [];

    // Pool of cleared List<int> objects ready for reuse, avoiding per-frame allocation.
    private readonly Stack<List<int>> _listPool = new();

    internal void Clear()
    {
        foreach (var key in _activeCells)
        {
            var list = _cells[key];
            list.Clear();
            _listPool.Push(list);
            _cells.Remove(key);
        }
        _activeCells.Clear();
    }

    internal void Insert(int id, Vector2 min, Vector2 max)
    {
        if (
            !float.IsFinite(min.X)
            || !float.IsFinite(min.Y)
            || !float.IsFinite(max.X)
            || !float.IsFinite(max.Y)
        )
            return;

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
            return;

        var minCx = (int)fMinCx;
        var minCy = (int)fMinCy;
        var maxCx = (int)fMaxCx;
        var maxCy = (int)fMaxCy;

        for (var cx = minCx; cx <= maxCx; cx++)
        {
            for (var cy = minCy; cy <= maxCy; cy++)
            {
                var key = (cx, cy);
                if (!_cells.TryGetValue(key, out var list))
                {
                    list = _listPool.Count > 0 ? _listPool.Pop() : [];
                    _cells[key] = list;
                }
                if (list.Count == 0)
                    _activeCells.Add(key);
                list.Add(id);
            }
        }
    }

    internal void GetCandidatePairs(HashSet<(int A, int B)> results)
    {
        foreach (var key in _activeCells)
        {
            var list = _cells[key];
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if (a < b)
                        results.Add((a, b));
                    else
                        results.Add((b, a));
                }
            }
        }
    }
}
