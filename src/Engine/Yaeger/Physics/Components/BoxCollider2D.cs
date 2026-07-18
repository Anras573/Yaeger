using System.Numerics;

namespace Yaeger.Physics.Components;

/// <summary>
/// Axis-aligned box collider for 2D physics.
/// The box is centered on the entity's Transform2D.Position, offset by <see cref="Offset"/>.
/// </summary>
public struct BoxCollider2D
{
    /// <summary>
    /// Bitmask meaning "collides with every layer" — the default for <see cref="CollidesWith"/>.
    /// </summary>
    public const uint AllLayers = uint.MaxValue;

    /// <summary>
    /// Full width and height of the box.
    /// </summary>
    public Vector2 Size;

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
    /// When <c>true</c>, this box behaves as a one-way ("jump-through") platform: a contact is
    /// only resolved when the other body approaches from the <see cref="SurfaceDirection"/> side
    /// and is not moving against that direction — e.g. a body jumping up through the platform's
    /// underside passes through, but the same body falling onto its top surface lands normally.
    /// See <see cref="Systems.CollisionResolutionSystem"/> and <c>PhysicsWorld2D.DropThrough</c>.
    /// </summary>
    public bool OneWay;

    /// <summary>
    /// Unit vector pointing away from the platform's solid side. Only meaningful when
    /// <see cref="OneWay"/> is <c>true</c>. Defaults to <see cref="Vector2.UnitY"/> (up), the
    /// standard "solid on top, pass-through from below" platform.
    /// </summary>
    public Vector2 SurfaceDirection;

    /// <summary>
    /// Creates a box collider with the specified size and optional offset.
    /// </summary>
    /// <param name="size">Full width and height. Both components must be greater than zero.</param>
    /// <param name="offset">Offset from the entity's position. Defaults to zero.</param>
    /// <param name="layer">The collision layer this collider belongs to. Must be within [0, 31]. Defaults to 0.</param>
    /// <param name="collidesWith">Bitmask of layers this collider collides with. Defaults to <see cref="AllLayers"/>.</param>
    /// <param name="isTrigger">Whether this collider is a non-resolving trigger/sensor. Defaults to <c>false</c>.</param>
    /// <param name="oneWay">Whether this box is a one-way ("jump-through") platform. Defaults to <c>false</c>.</param>
    /// <param name="surfaceDirection">
    /// The platform's solid-side direction; normalized on construction. Defaults to
    /// <see cref="Vector2.UnitY"/> (up). Only meaningful when <paramref name="oneWay"/> is <c>true</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when size has non-positive components, <paramref name="layer"/> is outside
    /// [0, 31], or <paramref name="surfaceDirection"/> is provided with near-zero length.
    /// </exception>
    public BoxCollider2D(
        Vector2 size,
        Vector2? offset = null,
        int layer = 0,
        uint collidesWith = AllLayers,
        bool isTrigger = false,
        bool oneWay = false,
        Vector2? surfaceDirection = null
    )
    {
        if (size.X <= 0 || size.Y <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "Size components must be greater than zero."
            );
        ValidateLayer(layer);

        Size = size;
        Offset = offset ?? Vector2.Zero;
        Layer = layer;
        CollidesWith = collidesWith;
        IsTrigger = isTrigger;
        OneWay = oneWay;
        SurfaceDirection = NormalizeSurfaceDirection(surfaceDirection);
    }

    /// <summary>
    /// Creates a box collider with the specified width and height.
    /// </summary>
    /// <param name="width">Width of the box. Must be greater than zero.</param>
    /// <param name="height">Height of the box. Must be greater than zero.</param>
    /// <param name="layer">The collision layer this collider belongs to. Must be within [0, 31]. Defaults to 0.</param>
    /// <param name="collidesWith">Bitmask of layers this collider collides with. Defaults to <see cref="AllLayers"/>.</param>
    /// <param name="isTrigger">Whether this collider is a non-resolving trigger/sensor. Defaults to <c>false</c>.</param>
    /// <param name="oneWay">Whether this box is a one-way ("jump-through") platform. Defaults to <c>false</c>.</param>
    /// <param name="surfaceDirection">
    /// The platform's solid-side direction; normalized on construction. Defaults to
    /// <see cref="Vector2.UnitY"/> (up). Only meaningful when <paramref name="oneWay"/> is <c>true</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when width or height is less than or equal to zero, <paramref name="layer"/> is
    /// outside [0, 31], or <paramref name="surfaceDirection"/> is provided with near-zero length.
    /// </exception>
    public BoxCollider2D(
        float width,
        float height,
        int layer = 0,
        uint collidesWith = AllLayers,
        bool isTrigger = false,
        bool oneWay = false,
        Vector2? surfaceDirection = null
    )
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
        ValidateLayer(layer);

        Size = new Vector2(width, height);
        Offset = Vector2.Zero;
        Layer = layer;
        CollidesWith = collidesWith;
        IsTrigger = isTrigger;
        OneWay = oneWay;
        SurfaceDirection = NormalizeSurfaceDirection(surfaceDirection);
    }

    /// <summary>
    /// Half of the size, useful for AABB calculations.
    /// </summary>
    public readonly Vector2 HalfSize => Size / 2.0f;

    private static void ValidateLayer(int layer)
    {
        if (layer < 0 || layer > 31)
            throw new ArgumentOutOfRangeException(
                nameof(layer),
                layer,
                "Layer must be within [0, 31]."
            );
    }

    private static Vector2 NormalizeSurfaceDirection(Vector2? surfaceDirection)
    {
        if (surfaceDirection is not { } direction)
            return Vector2.UnitY;

        var lengthSq = direction.LengthSquared();
        if (lengthSq < 1e-10f || !float.IsFinite(lengthSq))
            throw new ArgumentOutOfRangeException(
                nameof(surfaceDirection),
                direction,
                "Surface direction must be a non-zero, finite vector."
            );

        return direction / MathF.Sqrt(lengthSq);
    }
}
