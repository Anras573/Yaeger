using System.Numerics;

namespace Yaeger.Physics.Components;

/// <summary>
/// Circle collider for 2D physics.
/// The circle is centered on the entity's Transform2D.Position, offset by <see cref="Offset"/>.
/// </summary>
public struct CircleCollider2D
{
    /// <summary>
    /// Bitmask meaning "collides with every layer" — the default for <see cref="CollidesWith"/>.
    /// </summary>
    public const uint AllLayers = uint.MaxValue;

    /// <summary>
    /// Radius of the circle.
    /// </summary>
    public float Radius;

    /// <summary>
    /// Offset from the entity's position to the center of the collider.
    /// </summary>
    public Vector2 Offset;

    /// <summary>
    /// The collision layer (bit index, [0, 31]) this collider belongs to. Defaults to 0.
    /// </summary>
    public int Layer;

    /// <summary>
    /// Bitmask of layers this collider collides with. Two colliders A and B are only tested
    /// against each other when both <c>A.CollidesWith</c> includes B's layer and
    /// <c>B.CollidesWith</c> includes A's layer (a symmetric check). Defaults to
    /// <see cref="AllLayers"/>, so an unconfigured collider collides with everything —
    /// matching pre-filtering behavior.
    /// </summary>
    public uint CollidesWith;

    /// <summary>
    /// When <c>true</c>, this collider still produces <see cref="Physics.CollisionManifold"/>s
    /// (and fires <c>PhysicsWorld2D.OnCollision</c>) but is skipped by
    /// <see cref="Systems.CollisionResolutionSystem"/> — useful for coins, checkpoints, goal
    /// flags, and other sensors that should be detected without physically resolving.
    /// </summary>
    public bool IsTrigger;

    /// <summary>
    /// Creates a circle collider with the specified radius and optional offset.
    /// </summary>
    /// <param name="radius">Radius of the circle. Must be greater than zero.</param>
    /// <param name="offset">Offset from the entity's position. Defaults to zero.</param>
    /// <param name="layer">The collision layer this collider belongs to. Must be within [0, 31]. Defaults to 0.</param>
    /// <param name="collidesWith">Bitmask of layers this collider collides with. Defaults to <see cref="AllLayers"/>.</param>
    /// <param name="isTrigger">Whether this collider is a non-resolving trigger/sensor. Defaults to <c>false</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when radius is less than or equal to zero, or <paramref name="layer"/> is outside
    /// [0, 31].
    /// </exception>
    public CircleCollider2D(
        float radius,
        Vector2? offset = null,
        int layer = 0,
        uint collidesWith = AllLayers,
        bool isTrigger = false
    )
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(radius),
                radius,
                "Radius must be greater than zero."
            );
        if (layer < 0 || layer > 31)
            throw new ArgumentOutOfRangeException(
                nameof(layer),
                layer,
                "Layer must be within [0, 31]."
            );

        Radius = radius;
        Offset = offset ?? Vector2.Zero;
        Layer = layer;
        CollidesWith = collidesWith;
        IsTrigger = isTrigger;
    }
}
