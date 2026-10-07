using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Shared half-texel UV inset used by sprite sheets, tilemaps, and particle flipbooks so
/// spacing-free sheets never sample a neighbouring cell at non-integer scales.
/// </summary>
public static class UvInset
{
    /// <summary>
    /// Pulls each edge of the rectangle inward by <paramref name="texelInset"/> texels, capped at
    /// a quarter of the rectangle so it never collapses. A zero/negative
    /// <paramref name="textureSize"/> or inset returns the exact edges.
    /// </summary>
    public static (Vector2 UvMin, Vector2 UvMax) Apply(
        Vector2 uvMin,
        Vector2 uvMax,
        Vector2 textureSize,
        float texelInset = 0.5f
    )
    {
        if (textureSize.X <= 0f || textureSize.Y <= 0f || !(texelInset > 0f))
            return (uvMin, uvMax);

        var inset = new Vector2(
            Math.Min(texelInset / textureSize.X, (uvMax.X - uvMin.X) * 0.25f),
            Math.Min(texelInset / textureSize.Y, (uvMax.Y - uvMin.Y) * 0.25f)
        );
        return (uvMin + inset, uvMax - inset);
    }
}
