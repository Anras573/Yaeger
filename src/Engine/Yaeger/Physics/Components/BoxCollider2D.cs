using System.Numerics;

namespace Yaeger.Physics.Components;

/// <summary>
/// Axis-aligned box collider for 2D physics.
/// The box is centered on the entity's Transform2D.Position, offset by <see cref="Offset"/>.
/// </summary>
public struct BoxCollider2D
{
    /// <summary>
    /// Full width and height of the box.
    /// </summary>
    public Vector2 Size;

    /// <summary>
    /// Offset from the entity's position to the center of the collider.
    /// </summary>
    public Vector2 Offset;

    /// <summary>
    /// Creates a box collider with the specified size and optional offset.
    /// </summary>
    /// <param name="size">Full width and height. Both components must be greater than zero.</param>
    /// <param name="offset">Offset from the entity's position. Defaults to zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when size has non-positive components.</exception>
    public BoxCollider2D(Vector2 size, Vector2? offset = null)
    {
        if (size.X <= 0 || size.Y <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "Size components must be greater than zero."
            );

        Size = size;
        Offset = offset ?? Vector2.Zero;
    }

    /// <summary>
    /// Creates a box collider with the specified width and height.
    /// </summary>
    /// <param name="width">Width of the box. Must be greater than zero.</param>
    /// <param name="height">Height of the box. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when width or height is less than or equal to zero.</exception>
    public BoxCollider2D(float width, float height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "Width must be greater than zero."
            );
        if (height <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                "Height must be greater than zero."
            );

        Size = new Vector2(width, height);
        Offset = Vector2.Zero;
    }

    /// <summary>
    /// Half of the size, useful for AABB calculations.
    /// </summary>
    public readonly Vector2 HalfSize => Size / 2.0f;
}
