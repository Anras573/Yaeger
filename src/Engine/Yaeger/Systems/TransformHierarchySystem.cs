using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Resolves world-space <see cref="Transform2D"/>/<see cref="Transform3D"/> values for entities
/// that carry a <see cref="Parent"/> plus a <see cref="LocalTransform2D"/>/<see cref="LocalTransform3D"/>,
/// by composing each entity's local transform with its resolved ancestor chain.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Transform2D"/>/<see cref="Transform3D"/> remain world-space everywhere else in the
/// engine — renderers, physics, and cameras never need to know an entity is parented. Only
/// hierarchy children carry the extra <see cref="LocalTransform2D"/>/<see cref="LocalTransform3D"/>
/// component; this system overwrites (or adds) their <see cref="Transform2D"/>/<see cref="Transform3D"/>
/// each update with the composed world value. A root entity (no <see cref="Parent"/>, or a
/// <see cref="Parent"/> without a matching local-transform component) is left untouched — its
/// <see cref="Transform2D"/>/<see cref="Transform3D"/> is authored directly, as before.
/// </para>
/// <para>
/// Composition is translate-rotate-scale without shear: a child's local position is scaled and
/// rotated by its parent's <i>world</i> scale/rotation before being offset by the parent's world
/// position, rotations add (2D) or multiply (3D), and scales multiply component-wise. This
/// matches <see cref="Transform2D.TransformMatrix"/>/<see cref="Transform3D.ModelMatrix"/>'s own
/// scale-then-rotate-then-translate composition exactly for position and rotation; like most
/// scene graphs, it does not model the shear that a mathematically exact matrix decomposition
/// would produce when non-uniform scale and rotation combine across multiple levels.
/// </para>
/// <para>
/// Run this system after whatever gameplay/physics code updates a parent's own
/// <see cref="Transform2D"/>/<see cref="Transform3D"/> and each child's local transform, and
/// before any render system reads <see cref="Transform2D"/>/<see cref="Transform3D"/>.
/// </para>
/// <para>
/// <b>Orphaning:</b> if a child's <see cref="Parent"/> no longer resolves to an entity with the
/// matching world transform component (the parent was destroyed, or never had one), the child is
/// orphaned to world-space: its <see cref="Parent"/> component is removed and its last computed
/// <see cref="Transform2D"/>/<see cref="Transform3D"/> is left as-is. It is not destroyed.
/// </para>
/// <para>
/// <b>Cycles:</b> a <see cref="Parent"/> chain that loops back on itself throws
/// <see cref="InvalidOperationException"/> rather than recursing forever.
/// </para>
/// </remarks>
public class TransformHierarchySystem(World world) : IUpdateSystem
{
    /// <inheritdoc/>
    public void Update(float deltaTime)
    {
        ResolveHierarchy<Transform2D, LocalTransform2D>(world, Compose2D);
        ResolveHierarchy<Transform3D, LocalTransform3D>(world, Compose3D);
    }

    private static void ResolveHierarchy<TWorldTransform, TLocalTransform>(
        World world,
        Func<TWorldTransform, TLocalTransform, TWorldTransform> compose
    )
        where TWorldTransform : struct
        where TLocalTransform : struct
    {
        var childToParent = new Dictionary<Entity, Entity>();
        foreach (var (entity, parent, _) in world.Query<Parent, TLocalTransform>())
            childToParent[entity] = parent.ParentEntity;

        if (childToParent.Count == 0)
            return;

        var order = new List<Entity>(childToParent.Count);
        var visited = new HashSet<Entity>();
        var visiting = new HashSet<Entity>();

        foreach (var entity in childToParent.Keys)
            Visit(entity, childToParent, visited, visiting, order);

        foreach (var entity in order)
        {
            var parentEntity = childToParent[entity];
            var local = world.GetComponent<TLocalTransform>(entity);

            if (!world.TryGetComponent<TWorldTransform>(parentEntity, out var parentWorld))
            {
                // Orphaned: the parent was destroyed, or never had a world transform of this
                // kind. Keep the child's last computed world transform and stop treating it as
                // a hierarchy child.
                world.RemoveComponent<Parent>(entity);
                continue;
            }

            world.AddComponent(entity, compose(parentWorld, local));
        }
    }

    private static void Visit(
        Entity entity,
        Dictionary<Entity, Entity> childToParent,
        HashSet<Entity> visited,
        HashSet<Entity> visiting,
        List<Entity> order
    )
    {
        if (visited.Contains(entity))
            return;

        if (!visiting.Add(entity))
            throw new InvalidOperationException(
                $"Cycle detected in Parent hierarchy at entity {entity.Id}."
            );

        // Only recurse into the parent if it is itself a hierarchy child that needs resolving
        // first; a parent without a matching local transform is a root, read as-is below.
        if (
            childToParent.TryGetValue(entity, out var parentEntity)
            && childToParent.ContainsKey(parentEntity)
        )
            Visit(parentEntity, childToParent, visited, visiting, order);

        visiting.Remove(entity);
        visited.Add(entity);
        order.Add(entity);
    }

    private static Transform2D Compose2D(Transform2D parentWorld, LocalTransform2D local)
    {
        var scaledLocalPosition = local.Position * parentWorld.Scale;
        var cos = MathF.Cos(parentWorld.Rotation);
        var sin = MathF.Sin(parentWorld.Rotation);
        var rotatedLocalPosition = new Vector2(
            scaledLocalPosition.X * cos - scaledLocalPosition.Y * sin,
            scaledLocalPosition.X * sin + scaledLocalPosition.Y * cos
        );

        return new Transform2D(
            parentWorld.Position + rotatedLocalPosition,
            parentWorld.Rotation + local.Rotation,
            parentWorld.Scale * local.Scale
        );
    }

    private static Transform3D Compose3D(Transform3D parentWorld, LocalTransform3D local)
    {
        var scaledLocalPosition = local.Position * parentWorld.Scale;
        var rotatedLocalPosition = Vector3.Transform(scaledLocalPosition, parentWorld.Rotation);

        return new Transform3D(
            parentWorld.Position + rotatedLocalPosition,
            Quaternion.Normalize(parentWorld.Rotation * local.Rotation),
            parentWorld.Scale * local.Scale
        );
    }
}
