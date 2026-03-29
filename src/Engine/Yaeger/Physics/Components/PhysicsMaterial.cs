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
    /// <param name="restitution">Bounciness. Must be in range [0, 1].</param>
    /// <param name="friction">Friction coefficient. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when restitution is outside [0, 1] or friction is negative.</exception>
    public PhysicsMaterial(float restitution, float friction)
    {
        if (restitution < 0.0f || restitution > 1.0f)
            throw new ArgumentOutOfRangeException(
                nameof(restitution),
                restitution,
                "Restitution must be between 0 and 1."
            );

        if (friction < 0.0f)
            throw new ArgumentOutOfRangeException(
                nameof(friction),
                friction,
                "Friction must be non-negative."
            );

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
