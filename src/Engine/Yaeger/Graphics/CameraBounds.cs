using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// A world-space rectangle that <see cref="Systems.CameraFollowSystem"/> clamps a
/// <see cref="Camera2D"/>'s visible span to, so the camera never shows past a level's edges.
/// Attach alongside <see cref="CameraFollow"/> (and <see cref="Camera2D"/>) on the same entity —
/// it has no effect on its own.
/// </summary>
/// <remarks>
/// The camera's visible half-extents at a given zoom and window aspect ratio A are
/// (A / Zoom, 1 / Zoom) — see <see cref="Camera2D"/>'s remarks. When the level is narrower than
/// the viewport on an axis (the bounds are smaller than the visible span), that axis is centered
/// on the bounds' own midpoint instead of clamped, since there's no position that would avoid
/// showing past the level's edge on that axis anyway.
/// </remarks>
public struct CameraBounds
{
    /// <summary>The bottom-left corner of the bounds, in world units.</summary>
    public Vector2 Min;

    /// <summary>The top-right corner of the bounds, in world units.</summary>
    public Vector2 Max;

    /// <summary>
    /// Creates bounds spanning [<paramref name="min"/>, <paramref name="max"/>].
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="min"/> is greater than <paramref name="max"/> on either axis.
    /// </exception>
    public CameraBounds(Vector2 min, Vector2 max)
    {
        if (min.X > max.X || min.Y > max.Y)
            throw new ArgumentException(
                $"Min ({min}) must not exceed Max ({max}) on either axis.",
                nameof(min)
            );

        Min = min;
        Max = max;
    }

    /// <summary>
    /// Derives bounds spanning a <see cref="Tilemap"/>'s full extent, given the tilemap entity's
    /// own <see cref="Transform2D"/> (whose position is the map's bottom-left corner, matching
    /// <see cref="Tilemap"/>'s own convention).
    /// </summary>
    public static CameraBounds FromTilemap(Tilemap tilemap, Transform2D transform)
    {
        var min = transform.Position;
        var max =
            min
            + new Vector2(tilemap.Width * tilemap.TileSize.X, tilemap.Height * tilemap.TileSize.Y);
        return new CameraBounds(min, max);
    }
}
