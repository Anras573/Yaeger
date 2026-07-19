using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// A texture-backed sprite. <see cref="FlipX"/>/<see cref="FlipY"/> mirror the rendered image
/// horizontally/vertically without touching <see cref="Transform2D"/> — the renderer implements
/// this by swapping the submitted quad's UV coordinates, so it stays on the same batched path as
/// an unflipped sprite (no extra draw calls, no change to collider/transform state that a
/// negative <see cref="Transform2D.Scale"/> would otherwise conflate with actual size).
/// </summary>
/// <remarks>
/// For an entity animated via <see cref="SpriteSheet"/> + <see cref="AnimationState"/> rather
/// than a plain <see cref="Sprite"/>, attach a <see cref="Sprite"/> alongside them purely to
/// carry <see cref="FlipX"/>/<see cref="FlipY"/> — <see cref="UnifiedRenderSystem"/> reads it for
/// flip state only; its <see cref="TexturePath"/> is not used for rendering in that case (the
/// <see cref="SpriteSheet"/>'s texture is authoritative).
/// </remarks>
public readonly struct Sprite(
    string texturePath,
    Color? tint = null,
    bool flipX = false,
    bool flipY = false
)
{
    public string TexturePath { get; } = texturePath;
    public Color Tint { get; } = tint ?? Color.White;

    /// <summary>Mirrors the sprite horizontally when <c>true</c>.</summary>
    public bool FlipX { get; } = flipX;

    /// <summary>Mirrors the sprite vertically when <c>true</c>.</summary>
    public bool FlipY { get; } = flipY;

    /// <summary>
    /// Swaps the U (if <paramref name="flipX"/>) and/or V (if <paramref name="flipY"/>) bounds of
    /// a UV rectangle, mirroring whatever region it selects — the whole texture (`(0,0)`–`(1,1)`)
    /// for a plain <see cref="Sprite"/>, or a single frame's sub-rectangle for a
    /// <see cref="SpriteSheet"/>. Swapping min/max (rather than reflecting around the sheet's
    /// own center) is what keeps this correct for a sub-rectangle: each corner of the quad still
    /// samples its own frame's texel range, just mirrored within it.
    /// </summary>
    public static (Vector2 UvMin, Vector2 UvMax) ApplyFlip(
        Vector2 uvMin,
        Vector2 uvMax,
        bool flipX,
        bool flipY
    )
    {
        if (flipX)
            (uvMin.X, uvMax.X) = (uvMax.X, uvMin.X);
        if (flipY)
            (uvMin.Y, uvMax.Y) = (uvMax.Y, uvMin.Y);

        return (uvMin, uvMax);
    }
}
