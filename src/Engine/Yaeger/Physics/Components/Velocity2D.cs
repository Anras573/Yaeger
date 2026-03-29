using System.Numerics;

namespace Yaeger.Physics.Components;

/// <summary>
/// Represents the velocity of a 2D physics body.
/// </summary>
public struct Velocity2D
{
    /// <summary>
    /// Linear velocity in units per second.
    /// </summary>
    public Vector2 Linear;

    /// <summary>
    /// Angular velocity in radians per second.
    /// </summary>
    public float Angular;

    /// <summary>
    /// Creates a velocity with the specified linear and angular components.
    /// </summary>
    public Velocity2D(Vector2 linear, float angular = 0.0f)
    {
        Linear = linear;
        Angular = angular;
    }

    /// <summary>
    /// Creates a velocity with only linear motion.
    /// </summary>
    public Velocity2D(float x, float y)
    {
        Linear = new Vector2(x, y);
        Angular = 0.0f;
    }

    /// <summary>
    /// Zero velocity.
    /// </summary>
    public static Velocity2D Zero => new(Vector2.Zero);
}
