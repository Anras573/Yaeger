namespace Yaeger.ECS;

public class World
{
    private int _nextEntityId = 1;
    private readonly HashSet<int> _entities = [];
    private readonly Dictionary<Type, object> _componentStores = new();
    private readonly Dictionary<Type, Action<int>> _removeDelegates = new();

    public Entity CreateEntity()
    {
        var id = _nextEntityId++;
        _entities.Add(id);
        return new Entity(id);
    }

    public void DestroyEntity(Entity entity)
    {
        _entities.Remove(entity.Id);
        foreach (var store in _componentStores.Values)
        {
            if (!_removeDelegates.TryGetValue(store.GetType(), out var removeDelegate))
            {
                var removeMethod = store.GetType().GetMethod("Remove") ?? throw new InvalidOperationException("Remove method not found");
                removeDelegate = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), store, removeMethod);
                _removeDelegates[store.GetType()] = removeDelegate;
            }
            removeDelegate(entity.Id);
        }
    }

    public void AddComponent<T>(Entity entity, T component) where T : struct
    {
        var store = GetStore<T>();
        store.Add(entity, component);
    }

    public bool RemoveComponent<T>(Entity entity) where T : struct
    {
        var store = GetStore<T>();
        return store.Remove(entity);
    }

    public bool TryGetComponent<T>(Entity entity, out T component) where T : struct
    {
        var store = GetStore<T>();
        if (store.TryGet(entity, out component))
            return true;
        component = default;
        return false;
    }

    public IEnumerable<Entity> Entities => _entities.Select(id => new Entity(id));

    public ComponentStorage<T> GetStore<T>() where T : struct
    {
        if (_componentStores.TryGetValue(typeof(T), out var store)) return (ComponentStorage<T>)store;
        store = new ComponentStorage<T>();
        _componentStores[typeof(T)] = store;
        return (ComponentStorage<T>)store;
    }
}