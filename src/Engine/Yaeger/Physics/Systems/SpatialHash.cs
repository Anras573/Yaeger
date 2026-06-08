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

    internal void Clear()
    {
        _cells.Clear();
    }

    internal void Insert(int id, Vector2 min, Vector2 max)
    {
        var minCx = (int)MathF.Floor(min.X / cellSize);
        var minCy = (int)MathF.Floor(min.Y / cellSize);
        var maxCx = (int)MathF.Floor(max.X / cellSize);
        var maxCy = (int)MathF.Floor(max.Y / cellSize);

        for (var cx = minCx; cx <= maxCx; cx++)
        {
            for (var cy = minCy; cy <= maxCy; cy++)
            {
                var key = (cx, cy);
                if (!_cells.TryGetValue(key, out var list))
                {
                    list = [];
                    _cells[key] = list;
                }
                list.Add(id);
            }
        }
    }

    internal void GetCandidatePairs(HashSet<(int A, int B)> results)
    {
        foreach (var list in _cells.Values)
        {
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
