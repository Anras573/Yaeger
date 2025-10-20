namespace Yaeger.ECS;

public class ComponentStorage<T> where T : struct
{
    private readonly Dictionary<Entity, T> _components = new();

    public void Add(Entity entity, T component) => _components[entity] = component;
    public bool Remove(Entity entity) => _components.Remove(entity);
    public bool TryGet(Entity entity, out T component) => _components.TryGetValue(entity, out component);
    public IEnumerable<KeyValuePair<Entity, T>> All() => _components;
}