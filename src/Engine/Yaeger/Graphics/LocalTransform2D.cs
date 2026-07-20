using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// The transform of an entity relative to its <see cref="Yaeger.ECS.Parent"/>, in the parent's
/// local space. Paired with <see cref="Yaeger.ECS.Parent"/>, this drives
/// <see cref="Systems.TransformHierarchySystem"/>, which composes it with the ancestor chain and
/// writes the result into the entity's <see cref="Transform2D"/> — <see cref="Transform2D"/>
/// stays world-space everywhere else in the engine (renderers, physics, camera), so only
/// hierarchy children need this extra component.
/// </summary>
public struct LocalTransform2D(Vector2 position, float rotation = 0.0f, Vector2? scale = null)
{
    public Vector2 Position = position;
    public float Rotation = rotation;
    public Vector2 Scale = scale ?? Vector2.One;
}
