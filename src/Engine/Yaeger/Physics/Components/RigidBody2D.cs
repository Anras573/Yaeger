namespace Yaeger.Physics.Components;

/// <summary>
/// Defines the physical properties of a 2D rigid body.
/// </summary>
public struct RigidBody2D
{
    /// <summary>
    /// Mass of the body in kilograms. Must be positive for dynamic bodies.
    /// </summary>
    public float Mass;

    /// <summary>
    /// Precomputed inverse mass (1 / Mass). Zero for static and kinematic bodies.
    /// For dynamic bodies, this is always positive since mass must be &gt; 0.
    /// </summary>
    public float InverseMass;

    /// <summary>
    /// Multiplier for gravity applied to this body. Default is 1.0.
    /// Set to 0.0 to disable gravity for this body.
    /// </summary>
    public float GravityScale;

    /// <summary>
    /// Linear drag coefficient. Reduces velocity over time to simulate air resistance.
    /// 0.0 = no drag. Higher values = more resistance.
    /// </summary>
    public float LinearDrag;

    /// <summary>
    /// Determines how the body behaves in the simulation.
    /// </summary>
    public BodyType Type;

    /// <summary>
    /// Creates a dynamic rigid body with the specified mass.
    /// </summary>
    /// <param name="mass">Mass in kilograms. Must be greater than zero.</param>
    /// <param name="gravityScale">Multiplier for gravity. Default is 1.0.</param>
    /// <param name="linearDrag">Linear drag coefficient. Must be non-negative. Default is 0.0 (no drag).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when mass is less than or equal to zero, or linearDrag is negative.</exception>
    public static RigidBody2D CreateDynamic(
        float mass,
        float gravityScale = 1.0f,
        float linearDrag = 0.0f
    )
    {
        if (mass <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(mass),
                mass,
                "Mass must be greater than zero for dynamic bodies."
            );

        if (linearDrag < 0)
            throw new ArgumentOutOfRangeException(
                nameof(linearDrag),
                linearDrag,
                "Linear drag must be non-negative."
            );

        return new RigidBody2D
        {
            Mass = mass,
            InverseMass = 1.0f / mass,
            GravityScale = gravityScale,
            LinearDrag = linearDrag,
            Type = BodyType.Dynamic,
        };
    }

    /// <summary>
    /// Creates a static rigid body (infinite mass, immovable).
    /// </summary>
    public static RigidBody2D CreateStatic()
    {
        return new RigidBody2D
        {
            Mass = 0.0f,
            InverseMass = 0.0f,
            GravityScale = 0.0f,
            LinearDrag = 0.0f,
            Type = BodyType.Static,
        };
    }

    /// <summary>
    /// Creates a kinematic rigid body (moved manually, pushes dynamic bodies).
    /// </summary>
    public static RigidBody2D CreateKinematic()
    {
        return new RigidBody2D
        {
            Mass = 0.0f,
            InverseMass = 0.0f,
            GravityScale = 0.0f,
            LinearDrag = 0.0f,
            Type = BodyType.Kinematic,
        };
    }
}
