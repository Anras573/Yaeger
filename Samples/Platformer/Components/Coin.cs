using System.Numerics;

namespace Platformer.Components;

/// <summary>
/// Marks an entity as a collectible coin. <see cref="HalfSize"/> is used for the AABB overlap
/// test against the player's <see cref="Yaeger.Physics.Components.CharacterController2D"/> box —
/// coins aren't part of the physics collider pipeline since the player bypasses it entirely.
/// </summary>
public struct Coin(Vector2 halfSize)
{
    public Vector2 HalfSize = halfSize;
}
