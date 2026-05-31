using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Marks an entity as a parallax background layer. The layer's world-space position is
/// recomputed each frame from <see cref="BasePosition"/> and the active camera position:
/// <c>worldPos = BasePosition + cameraPos * (1 - ScrollFactor)</c>.
///
/// ScrollFactor of 0 = screen-fixed (sky/far background that never scrolls).
/// ScrollFactor of 1 = world-fixed (same behaviour as a regular game entity).
/// Values between 0 and 1 create the classic parallax depth effect.
/// </summary>
public struct ParallaxLayer(float scrollFactorX = 0.5f, float scrollFactorY = 0f)
{
    public ParallaxLayer()
        : this(0.5f, 0f) { }

    /// <summary>Horizontal parallax factor. 0 = screen-fixed, 1 = world-fixed.</summary>
    public float ScrollFactorX = scrollFactorX;

    /// <summary>Vertical parallax factor. 0 = screen-fixed, 1 = world-fixed.</summary>
    public float ScrollFactorY = scrollFactorY;

    /// <summary>
    /// World-space anchor position for this layer at camera origin (0, 0).
    /// Defaults to <see cref="Vector2.Zero"/>.
    /// </summary>
    public Vector2 BasePosition;
}
