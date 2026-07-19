using System.Numerics;

namespace Platformer.Components;

/// <summary>
/// Marks an entity as a stompable enemy. <see cref="HalfSize"/> is used for the overlap test
/// against the player, same reasoning as <see cref="Coin"/>.
/// </summary>
public struct Enemy(Vector2 halfSize)
{
    public Vector2 HalfSize = halfSize;
}
