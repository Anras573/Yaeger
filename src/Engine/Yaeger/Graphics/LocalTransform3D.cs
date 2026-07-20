using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// The transform of an entity relative to its <see cref="Yaeger.ECS.Parent"/>, in the parent's
/// local space. Paired with <see cref="Yaeger.ECS.Parent"/>, this drives
/// <see cref="Systems.TransformHierarchySystem"/>, which composes it with the ancestor chain and
/// writes the result into the entity's <see cref="Transform3D"/> — <see cref="Transform3D"/>
/// stays world-space everywhere else in the engine (renderers, physics, lighting), so only
/// hierarchy children need this extra component.
/// </summary>
public record struct LocalTransform3D(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    public static LocalTransform3D Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);
}
