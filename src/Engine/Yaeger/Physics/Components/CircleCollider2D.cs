using System.Numerics;

namespace Yaeger.Physics.Components;

/// <summary>
/// Circle collider for 2D physics.
/// The circle is centered on the entity's Transform2D.Position, offset by <see cref="Offset"/>.
/// </summary>
public struct CircleCollider2D
{
    /// <summary>
    /// Radius of the circle.
    /// </summary>
    public float Radius;

    /// <summary>
    /// Offset from the entity's position to the center of the collider.
    /// </summary>
    public Vector2 Offset;

    /// <summary>
    /// Creates a circle collider with the specified radius and optional offset.
    /// </summary>
    public CircleCollider2D(float radius, Vector2? offset = null)
    {
        Radius = radius;
        Offset = offset ?? Vector2.Zero;
    }
}
