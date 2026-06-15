namespace Yaeger.ECS;

public class ComponentStorage<T> : IComponentStore
    where T : struct
{
    private readonly Dictionary<Entity, T> _components = new();

    public int Count => _components.Count;

    public void Add(Entity entity, T component) => _components[entity] = component;

    public bool Remove(Entity entity) => _components.Remove(entity);

    public bool TryGet(Entity entity, out T component) =>
        _components.TryGetValue(entity, out component);

    public T Get(Entity entity) => _components[entity];

    public IEnumerable<KeyValuePair<Entity, T>> All() => _components;

    /// <summary>
    /// Returns a non-allocating struct enumerator over the (entity, component) pairs, enabling
    /// <c>foreach</c> directly over the storage. Unlike <see cref="All"/> — which surfaces the
    /// pairs through the boxing <see cref="IEnumerable{T}"/> interface — this avoids a per-call
    /// heap allocation, which matters for stores iterated every frame.
    /// </summary>
    public Dictionary<Entity, T>.Enumerator GetEnumerator() => _components.GetEnumerator();
}
