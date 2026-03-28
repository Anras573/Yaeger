namespace Yaeger.Physics.Components;

/// <summary>
/// Defines the physical surface properties of a body.
/// This component is dimensionality-agnostic and can be reused for both 2D and 3D physics.
/// </summary>
public struct PhysicsMaterial
{
    /// <summary>
    /// Bounciness of the surface. 0.0 = no bounce, 1.0 = perfectly elastic.
    /// </summary>
    public float Restitution;

    /// <summary>
    /// Friction coefficient. 0.0 = frictionless, higher values = more friction.
    /// </summary>
    public float Friction;

    /// <summary>
    /// Creates a new physics material with the specified properties.
    /// </summary>
    public PhysicsMaterial(float restitution, float friction)
    {
        Restitution = restitution;
        Friction = friction;
    }

    /// <summary>
    /// Default material with moderate bounce and friction.
    /// </summary>
    public static PhysicsMaterial Default => new(0.3f, 0.4f);

    /// <summary>
    /// Perfectly bouncy material with no friction.
    /// </summary>
    public static PhysicsMaterial Bouncy => new(1.0f, 0.0f);

    /// <summary>
    /// No bounce, high friction. Useful for floors and walls.
    /// </summary>
    public static PhysicsMaterial Sticky => new(0.0f, 1.0f);
}
