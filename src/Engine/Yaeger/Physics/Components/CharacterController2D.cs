using System.Numerics;
using Yaeger.ECS;

namespace Yaeger.Physics.Components;

/// <summary>
/// A kinematic, box-shaped character controller: moved by <see cref="Systems.CharacterControllerSystem"/>
/// via axis-separated sweep-and-slide against solid <see cref="BoxCollider2D"/> obstacles
/// (including tilemap-generated colliders and one-way platforms), instead of the impulse-based
/// <see cref="Systems.CollisionResolutionSystem"/>.
/// </summary>
/// <remarks>
/// Pair with a <see cref="Transform2D"/> and a <see cref="Velocity2D"/> — the latter is both the
/// input (desired movement per second, set by game/input code) and the output (zeroed along any
/// axis that hits a contact). Do not also add a <see cref="BoxCollider2D"/> or
/// <see cref="CircleCollider2D"/> to a controller entity: it would be redundantly processed by
/// the impulse pipeline, which this component is designed to bypass entirely.
/// </remarks>
public struct CharacterController2D
{
    /// <summary>
    /// Full width and height of the controller's box.
    /// </summary>
    public Vector2 Size;

    /// <summary>
    /// Offset from the entity's position to the center of the controller's box.
    /// </summary>
    public Vector2 Offset;

    /// <summary>
    /// Multiplier applied to <see cref="Systems.CharacterControllerSystem.Gravity"/> for this
    /// controller, independent of any <c>PhysicsWorld2D.Gravity</c> in use elsewhere — lets a
    /// player's jump arc be tuned without affecting impulse-resolved bodies. Defaults to 1.0.
    /// </summary>
    public float GravityScale;

    /// <summary>
    /// Maximum ledge height (in world units) the controller climbs automatically while moving
    /// horizontally, instead of stopping against it. Defaults to 0 (no step-up).
    /// </summary>
    public float StepHeight;

    /// <summary>
    /// The collision layer (bit index, [0, 31]) this controller belongs to. Defaults to 0.
    /// </summary>
    public int Layer;

    /// <summary>
    /// Bitmask of layers this controller collides with, matching <see cref="BoxCollider2D.CollidesWith"/>'s
    /// symmetric semantics. Defaults to <see cref="BoxCollider2D.AllLayers"/>.
    /// </summary>
    public uint CollidesWith;

    /// <summary>
    /// Whether the controller was resting on solid ground at the end of the last
    /// <see cref="Systems.CharacterControllerSystem.Update"/> step. Written by the system —
    /// treat as read-only from game code.
    /// </summary>
    public bool IsGrounded;

    /// <summary>Whether the controller is currently touching a wall to its left. Written by the system.</summary>
    public bool IsTouchingWallLeft;

    /// <summary>Whether the controller is currently touching a wall to its right. Written by the system.</summary>
    public bool IsTouchingWallRight;

    /// <summary>Whether the controller is currently touching a ceiling above it. Written by the system.</summary>
    public bool IsTouchingCeiling;

    /// <summary>
    /// The contact normal of the ground the controller is standing on, or <see cref="Vector2.Zero"/>
    /// when <see cref="IsGrounded"/> is <c>false</c>. Written by the system.
    /// </summary>
    public Vector2 GroundNormal;

    /// <summary>
    /// The entity the controller is currently resting on, or <c>null</c> when
    /// <see cref="IsGrounded"/> is <c>false</c>. Written by the system. Used to carry the
    /// controller along with a moving ground entity's own displacement each step — see
    /// <see cref="Systems.CharacterControllerSystem"/>'s remarks — so a controller doesn't slide
    /// off a moving platform (elevator, ferry) it's standing still on.
    /// </summary>
    public Entity? GroundEntity;

    /// <summary>
    /// Creates a character controller with the specified box size and optional offset.
    /// </summary>
    /// <param name="size">Full width and height. Both components must be greater than zero.</param>
    /// <param name="offset">Offset from the entity's position. Defaults to zero.</param>
    /// <param name="gravityScale">Multiplier for <see cref="Systems.CharacterControllerSystem.Gravity"/>. Defaults to 1.0.</param>
    /// <param name="stepHeight">Maximum auto-climbable ledge height. Must be non-negative. Defaults to 0.</param>
    /// <param name="layer">The collision layer this controller belongs to. Must be within [0, 31]. Defaults to 0.</param>
    /// <param name="collidesWith">Bitmask of layers this controller collides with. Defaults to <see cref="BoxCollider2D.AllLayers"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when size has non-positive components, <paramref name="stepHeight"/> is negative,
    /// or <paramref name="layer"/> is outside [0, 31].
    /// </exception>
    public CharacterController2D(
        Vector2 size,
        Vector2? offset = null,
        float gravityScale = 1.0f,
        float stepHeight = 0f,
        int layer = 0,
        uint collidesWith = BoxCollider2D.AllLayers
    )
    {
        if (size.X <= 0 || size.Y <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "Size components must be greater than zero."
            );
        if (stepHeight < 0 || !float.IsFinite(stepHeight))
            throw new ArgumentOutOfRangeException(
                nameof(stepHeight),
                stepHeight,
                "Step height must be a non-negative finite value."
            );
        if (layer < 0 || layer > 31)
            throw new ArgumentOutOfRangeException(
                nameof(layer),
                layer,
                "Layer must be within [0, 31]."
            );

        Size = size;
        Offset = offset ?? Vector2.Zero;
        GravityScale = gravityScale;
        StepHeight = stepHeight;
        Layer = layer;
        CollidesWith = collidesWith;
        IsGrounded = false;
        IsTouchingWallLeft = false;
        IsTouchingWallRight = false;
        IsTouchingCeiling = false;
        GroundNormal = Vector2.Zero;
        GroundEntity = null;
    }

    /// <summary>
    /// Creates a character controller with the specified width and height.
    /// </summary>
    /// <param name="width">Width of the box. Must be greater than zero.</param>
    /// <param name="height">Height of the box. Must be greater than zero.</param>
    /// <param name="gravityScale">Multiplier for <see cref="Systems.CharacterControllerSystem.Gravity"/>. Defaults to 1.0.</param>
    /// <param name="stepHeight">Maximum auto-climbable ledge height. Must be non-negative. Defaults to 0.</param>
    /// <param name="layer">The collision layer this controller belongs to. Must be within [0, 31]. Defaults to 0.</param>
    /// <param name="collidesWith">Bitmask of layers this controller collides with. Defaults to <see cref="BoxCollider2D.AllLayers"/>.</param>
    public CharacterController2D(
        float width,
        float height,
        float gravityScale = 1.0f,
        float stepHeight = 0f,
        int layer = 0,
        uint collidesWith = BoxCollider2D.AllLayers
    )
        : this(new Vector2(width, height), null, gravityScale, stepHeight, layer, collidesWith) { }

    /// <summary>
    /// Half of the size, useful for AABB calculations.
    /// </summary>
    public readonly Vector2 HalfSize => Size / 2.0f;
}
