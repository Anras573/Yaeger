using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Component that references a sprite sheet texture and describes how it is divided
/// into a uniform grid of equally-sized frames.
/// </summary>
/// <remarks>
/// The sheet is assumed to be a single row of <paramref name="columns"/> frames laid out
/// left-to-right (rows are supported for multi-row sheets via <paramref name="rows"/>).
/// Frames are indexed left-to-right, top-to-bottom starting at 0.
/// </remarks>
public readonly struct SpriteSheet
{
    /// <summary>Gets the path to the sprite sheet texture.</summary>
    public string TexturePath { get; }

    /// <summary>Gets the number of columns (frames across) in the sheet.</summary>
    public int Columns { get; }

    /// <summary>Gets the number of rows in the sheet.</summary>
    public int Rows { get; }

    /// <summary>
    /// Gets the total number of frames in the animation.
    /// Defaults to <c>Columns * Rows</c> when not specified explicitly, but can be
    /// set to a smaller value when the last row is not fully populated.
    /// </summary>
    public int FrameCount { get; }

    /// <summary>Gets the tint colour applied when rendering this sprite sheet. Defaults to white (no tint).</summary>
    public Color Tint { get; }

    /// <summary>
    /// Initializes a new <see cref="SpriteSheet"/>.
    /// </summary>
    /// <param name="texturePath">
    /// Path to the sprite sheet image file. <see cref="IRenderSurface.SolidTexturePath"/> (<c>""</c>)
    /// is accepted for a single-frame sheet and renders as a flat colour.
    /// </param>
    /// <param name="columns">Number of equally-wide columns in the sheet.</param>
    /// <param name="rows">Number of equally-tall rows in the sheet. Defaults to 1.</param>
    /// <param name="frameCount">
    /// Total number of valid frames. Defaults to <paramref name="columns"/> ×
    /// <paramref name="rows"/> when not specified.
    /// </param>
    /// <param name="tint">
    /// Tint colour applied during rendering. Defaults to <see cref="Color.White"/> (no tint).
    /// </param>
    public SpriteSheet(
        string texturePath,
        int columns,
        int rows = 1,
        int? frameCount = null,
        Color? tint = null
    )
    {
        ArgumentNullException.ThrowIfNull(texturePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);

        var maxFrames = columns * rows;
        var resolvedFrameCount = frameCount ?? maxFrames;
        ArgumentOutOfRangeException.ThrowIfLessThan(resolvedFrameCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resolvedFrameCount, maxFrames);

        // The solid-colour path is a 1x1 white texture, so it only makes sense as a single frame.
        if (texturePath.Length == 0 && resolvedFrameCount > 1)
        {
            throw new ArgumentException(
                "An empty (solid-colour) texture path is only valid for a single-frame sheet.",
                nameof(texturePath)
            );
        }

        TexturePath = texturePath;
        Columns = columns;
        Rows = rows;
        FrameCount = resolvedFrameCount;
        Tint = tint ?? Color.White;
    }

    /// <summary>
    /// Returns the normalised UV rectangle for the given zero-based frame index.
    /// </summary>
    /// <param name="frameIndex">Zero-based frame index (left-to-right, top-to-bottom).</param>
    /// <returns>
    /// A tuple of (<c>uvMin</c>, <c>uvMax</c>) where both are normalised [0, 1] coordinates.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="frameIndex"/> is outside [0, <see cref="FrameCount"/>).
    /// </exception>
    public (Vector2 UvMin, Vector2 UvMax) GetFrameUv(int frameIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, FrameCount);

        var col = frameIndex % Columns;
        var row = frameIndex / Columns;

        var frameWidth = 1f / Columns;
        var frameHeight = 1f / Rows;

        var uMin = col * frameWidth;
        var uMax = uMin + frameWidth;
        var vMax = 1f - (row * frameHeight);
        var vMin = vMax - frameHeight;

        var uvMin = new Vector2(uMin, vMin);
        var uvMax = new Vector2(uMax, vMax);

        return (uvMin, uvMax);
    }

    /// <summary>
    /// Like <see cref="GetFrameUv(int)"/>, but pulls each edge inward by <paramref name="texelInset"/>
    /// texels so bilinear taps and rounding at non-integer scales never sample a neighbouring
    /// cell of a spacing-free sheet. The inset is capped at a quarter of the cell so a frame
    /// never collapses. A zero/negative <paramref name="textureSize"/> or inset returns the exact
    /// cell edges.
    /// </summary>
    /// <param name="frameIndex">Zero-based frame index.</param>
    /// <param name="textureSize">Pixel size of the sheet texture.</param>
    /// <param name="texelInset">Inset per edge in texels (default half a texel).</param>
    public (Vector2 UvMin, Vector2 UvMax) GetFrameUv(
        int frameIndex,
        Vector2 textureSize,
        float texelInset = 0.5f
    )
    {
        var (uvMin, uvMax) = GetFrameUv(frameIndex);
        if (textureSize.X <= 0f || textureSize.Y <= 0f || !(texelInset > 0f))
            return (uvMin, uvMax);

        var inset = new Vector2(
            Math.Min(texelInset / textureSize.X, (uvMax.X - uvMin.X) * 0.25f),
            Math.Min(texelInset / textureSize.Y, (uvMax.Y - uvMin.Y) * 0.25f)
        );
        return (uvMin + inset, uvMax - inset);
    }
}
