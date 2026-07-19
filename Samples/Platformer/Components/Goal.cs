using System.Numerics;

namespace Platformer.Components;

/// <summary>Marks the win-condition entity (the goal flag).</summary>
public struct Goal(Vector2 halfSize)
{
    public Vector2 HalfSize = halfSize;
}
