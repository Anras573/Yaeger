namespace Yaeger.ECS;

public class World
{
    private int _nextEntityId = 1;
    private readonly HashSet<Entity> _entities = [];
    private readonly Dictionary<Type, object> _componentStores = new();
    private readonly Dictionary<Type, Action<int>> _removeDelegates = new();
    private readonly Dictionary<string, Entity> _taggedEntities = new();
    private readonly Dictionary<Entity, string> _entitiesByTag = new();

    public Entity CreateEntity()
    {
        var id = _nextEntityId++;
        var entity = new Entity(id);
        _entities.Add(entity);
        return entity;
    }

    public Entity CreateEntity(string tag)
    {
        var entity = CreateEntity();
        _taggedEntities[tag] = entity;
        _entitiesByTag[entity] = tag;
        return entity;
    }

    public bool TryGetEntity(string tag, out Entity entity) => _taggedEntities.TryGetValue(tag, out entity);
    public Entity GetEntity(string tag) => _taggedEntities[tag];

    public void DestroyEntity(Entity entity)
    {
        if (_entitiesByTag.TryGetValue(entity, out var tag))
        {
            _taggedEntities.Remove(tag);
            _entitiesByTag.Remove(entity);
        }

        _entities.Remove(entity);
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

    public T GetComponent<T>(Entity entity) where T : struct => GetStore<T>().Get(entity);

    public IEnumerable<Entity> Entities => _entities;

    public ComponentStorage<T> GetStore<T>() where T : struct
    {
        if (_componentStores.TryGetValue(typeof(T), out var store)) return (ComponentStorage<T>)store;
        store = new ComponentStorage<T>();
        _componentStores[typeof(T)] = store;
        return (ComponentStorage<T>)store;
    }
}