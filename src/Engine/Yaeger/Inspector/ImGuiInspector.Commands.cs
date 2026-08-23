using System.Reflection;
using Yaeger.ECS;

namespace Yaeger.Inspector;

// The deferred command queue: edits and removals raised while drawing ImGui widgets are queued
// here and applied once per frame by FlushPendingCommands (called from Render — see
// ImGuiInspector.cs), rather than mutating the World mid-iteration.
public sealed partial class ImGuiInspector
{
    // Deferred commands — mutations run after all ImGui draw calls to avoid iterator invalidation.
    // A List is used so multiple operations queued in the same frame (e.g. remove + add) all execute.
    private Entity? _pendingDestroyEntity;
    private readonly List<Action<World>> _pendingWorldOps = [];

    private static readonly MethodInfo WorldRemoveMethod = typeof(World).GetMethod(
        nameof(World.RemoveComponent)
    )!;

    private static readonly MethodInfo WorldTryGetComponentMethod = typeof(World).GetMethod(
        nameof(World.TryGetComponent)
    )!;

    /// <summary>
    /// Checks whether <paramref name="entity"/> carries the component handled by
    /// <paramref name="serializer"/>.  When <see cref="IComponentSerializer.ComponentType"/>
    /// is known, reflection is used for an authoritative presence check — this handles
    /// serializers that return <c>null</c> from TrySerialize even when the entity has the
    /// component (e.g. load-only / write-unsupported serializers).  Falls back to
    /// TrySerialize for serializers that don't expose a ComponentType.
    /// </summary>
    private bool EntityHasComponent(Entity entity, IComponentSerializer serializer)
    {
        if (serializer.ComponentType is { } type && type.IsValueType)
        {
            var method = WorldTryGetComponentMethod.MakeGenericMethod(type);
            // Pass a boxed default for the out parameter; null can misfire for value types
            var args = new object?[] { entity, Activator.CreateInstance(type) };
            return (bool)method.Invoke(_world, args)!;
        }

        return serializer.TrySerialize(_world, entity) != null;
    }

    private void ScheduleRemove(Entity entity, Type componentType)
    {
        // componentType must be a struct (World.RemoveComponent<T> where T : struct)
        if (!componentType.IsValueType)
            return;

        _pendingWorldOps.Add(w =>
        {
            var method = WorldRemoveMethod.MakeGenericMethod(componentType);
            method.Invoke(w, [entity]);
        });
    }

    private void FlushPendingCommands()
    {
        // World ops run first so that an Add queued in the same frame as Destroy
        // doesn't create zombie components on an entity that no longer exists.
        foreach (var op in _pendingWorldOps)
            op(_world);
        _pendingWorldOps.Clear();

        if (_pendingDestroyEntity.HasValue)
        {
            _world.DestroyEntity(_pendingDestroyEntity.Value);
            _pendingDestroyEntity = null;
        }
    }
}
