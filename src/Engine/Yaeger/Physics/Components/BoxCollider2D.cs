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
    public BoxCollider2D(Vector2 size, Vector2? offset = null)
    {
        Size = size;
        Offset = offset ?? Vector2.Zero;
    }

    /// <summary>
    /// Creates a box collider with the specified width and height.
    /// </summary>
    public BoxCollider2D(float width, float height)
    {
        Size = new Vector2(width, height);
        Offset = Vector2.Zero;
    }

    /// <summary>
    /// Half of the size, useful for AABB calculations.
    /// </summary>
    public readonly Vector2 HalfSize => Size / 2.0f;
}
