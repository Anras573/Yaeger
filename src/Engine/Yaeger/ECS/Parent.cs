namespace Yaeger.ECS;

/// <summary>
/// Marks an entity as a child of <see cref="ParentEntity"/>, driving <see cref="Systems.TransformHierarchySystem"/>
/// when paired with <see cref="Yaeger.Graphics.LocalTransform2D"/> or <see cref="Yaeger.Graphics.LocalTransform3D"/>.
/// </summary>
/// <remarks>
/// There is no separate child-list component — children are found by querying for <see cref="Parent"/>.
/// If <see cref="ParentEntity"/> is destroyed (or otherwise loses its world transform component),
/// the child is orphaned to world-space: it keeps its last computed world transform and
/// <see cref="TransformHierarchySystem"/> removes its <see cref="Parent"/> component on the next
/// update rather than destroying it. A parent chain that loops back on itself is invalid and
/// causes <see cref="Systems.TransformHierarchySystem.Update"/> to throw
/// <see cref="InvalidOperationException"/> rather than hang.
/// </remarks>
public struct Parent(Entity parentEntity)
{
    /// <summary>The entity this entity's transform is relative to.</summary>
    public Entity ParentEntity = parentEntity;
}
