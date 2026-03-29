namespace Yaeger.Physics.Components;

/// <summary>
/// Defines how a rigid body behaves in the physics simulation.
/// This enum is dimensionality-agnostic and can be reused for both 2D and 3D physics.
/// </summary>
public enum BodyType : byte
{
    /// <summary>
    /// Fully simulated body. Affected by forces, gravity, and collisions.
    /// </summary>
    Dynamic,

    /// <summary>
    /// Immovable body. Not affected by forces or collisions, but other bodies collide with it.
    /// Useful for walls, floors, and platforms.
    /// </summary>
    Static,

    /// <summary>
    /// Moved manually (e.g. by code or animation). Not affected by forces,
    /// but pushes dynamic bodies during collisions.
    /// Useful for moving platforms or elevators.
    /// </summary>
    Kinematic,
}
