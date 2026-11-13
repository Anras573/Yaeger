namespace Yaeger.ECS;

/// <summary>
/// Hands out a small, process-wide dense index per component type, so <see cref="World"/> can
/// find a store with an array read instead of a <c>Dictionary&lt;Type, …&gt;</c> lookup.
/// </summary>
internal static class ComponentTypeId<T>
    where T : struct
{
    internal static readonly int Id = ComponentTypeIdCounter.Next();
}

internal static class ComponentTypeIdCounter
{
    private static int _next = -1;

    internal static int Next() => Interlocked.Increment(ref _next);
}
