namespace Yaeger.ECS;

public class World
{
    private int _nextEntityId = 1;
    private readonly HashSet<Entity> _entities = [];
    private readonly Dictionary<Type, IComponentStore> _componentStores = new();
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
        ArgumentException.ThrowIfNullOrWhiteSpace(tag, nameof(tag));
        var entity = CreateEntity();
        // Clean up the previous entity's reverse mapping when a tag is reused so that
        // destroying the old entity does not accidentally remove the new entity's tag.
        if (_taggedEntities.TryGetValue(tag, out var previousEntity))
            _entitiesByTag.Remove(previousEntity);
        _taggedEntities[tag] = entity;
        _entitiesByTag[entity] = tag;
        return entity;
    }

    public bool TryGetEntity(string tag, out Entity entity) =>
        _taggedEntities.TryGetValue(tag, out entity);

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
            store.Remove(entity);
    }

    /// <summary>
    /// Destroys <paramref name="entity"/> and every entity reachable from it through
    /// <see cref="Parent"/> — children, grandchildren, and so on — including their tag
    /// registrations. Unlike <see cref="DestroyEntity"/> alone (which orphans children to
    /// world-space rather than cascading — see docs/hierarchy.md), this walks the whole subtree
    /// first and destroys every entity in it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The <see cref="Parent"/> chain beneath <paramref name="entity"/> loops back on itself,
    /// matching <see cref="Systems.TransformHierarchySystem"/>'s cycle guard — thrown instead of
    /// recursing/looping forever.
    /// </exception>
    public void DestroyHierarchy(Entity entity)
    {
        var childrenByParent = new Dictionary<Entity, List<Entity>>();
        foreach (var (child, parent) in GetStore<Parent>())
        {
            if (!childrenByParent.TryGetValue(parent.ParentEntity, out var siblings))
                childrenByParent[parent.ParentEntity] = siblings = [];
            siblings.Add(child);
        }

        // Collect the whole subtree before destroying anything: destruction mutates component
        // stores (including Parent's), so walking and destroying in the same pass would corrupt
        // the traversal. A node can only have one Parent, so childrenByParent partitions every
        // parented entity into exactly one list — the only way this breadth-first walk can ever
        // revisit a node is a genuine cycle in the chain beneath entity.
        var toDestroy = new List<Entity> { entity };
        var visited = new HashSet<Entity> { entity };
        var frontier = new Queue<Entity>();
        frontier.Enqueue(entity);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
                continue;

            foreach (var child in children)
            {
                if (!visited.Add(child))
                    throw new InvalidOperationException(
                        $"Cycle detected in Parent hierarchy at entity {child.Id}."
                    );

                toDestroy.Add(child);
                frontier.Enqueue(child);
            }
        }

        foreach (var member in toDestroy)
            DestroyEntity(member);
    }

    public void AddComponent<T>(Entity entity, T component)
        where T : struct
    {
        var store = GetStore<T>();
        store.Add(entity, component);
    }

    public bool RemoveComponent<T>(Entity entity)
        where T : struct
    {
        var store = GetStore<T>();
        return store.Remove(entity);
    }

    public bool TryGetComponent<T>(Entity entity, out T component)
        where T : struct
    {
        var store = GetStore<T>();
        if (store.TryGet(entity, out component))
            return true;
        component = default;
        return false;
    }

    public T GetComponent<T>(Entity entity)
        where T : struct => GetStore<T>().Get(entity);

    /// <summary>
    /// Instantiates a <see cref="Prefab"/> by creating a new entity and applying all of the
    /// prefab's component values to it.
    /// </summary>
    /// <param name="prefab">The prefab template to instantiate.</param>
    /// <param name="tag">
    /// An optional tag for the new entity.  When provided the entity is registered under
    /// this tag and can be looked up via <see cref="GetEntity"/> or <see cref="TryGetEntity"/>.
    /// A tag can only be bound to one entity at a time; if the tag is already in use, it is
    /// rebound to the new entity and the previous entity's reverse mapping is cleaned up.
    /// The tag must not be empty or whitespace.
    /// </param>
    /// <returns>The newly created entity with all prefab components applied.</returns>
    public Entity Instantiate(Prefab prefab, string? tag = null)
    {
        ArgumentNullException.ThrowIfNull(prefab);
        if (tag is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(tag, nameof(tag));
        var entity = tag is not null ? CreateEntity(tag) : CreateEntity();
        prefab.Apply(this, entity);
        return entity;
    }

    /// <summary>
    /// Spawns every entity described by <paramref name="scene"/> into the world, restoring
    /// tags and applying each entity's components in scene-file order.
    /// </summary>
    /// <returns>The created entities in the same order as the scene file.</returns>
    public IReadOnlyList<Entity> Instantiate(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return scene.Apply(this);
    }

    /// <summary>
    /// Spawns every entity described by <paramref name="scene"/>, the same as
    /// <see cref="Instantiate(Scene)"/>, but returns a <see cref="SceneInstance"/> handle that
    /// can later <see cref="SceneInstance.Unload"/> exactly these entities — a lift interior
    /// giving way to a hangar, a level transition, a menu returning to gameplay — without the
    /// caller keeping its own entity list around. Loading is additive: call this as many times
    /// as needed and unload each returned instance independently; nothing here assumes only one
    /// scene is ever loaded at a time. See docs/scenes.md.
    /// </summary>
    public SceneInstance LoadScene(Scene scene) => new(this, Instantiate(scene));

    /// <summary>
    /// The common scene-transition case: unloads <paramref name="previous"/> (if it isn't
    /// already unloaded, or <c>null</c> — there's nothing to tear down for a first load) and
    /// then loads <paramref name="next"/>. Unloading first means <paramref name="next"/> can
    /// safely reuse any tag <paramref name="previous"/> held, since <c>DestroyEntity</c> frees a
    /// tag's binding immediately.
    /// </summary>
    public SceneInstance SwapScene(SceneInstance? previous, Scene next)
    {
        previous?.Unload();
        return LoadScene(next);
    }

    public bool TryGetTag(
        Entity entity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? tag
    ) => _entitiesByTag.TryGetValue(entity, out tag);

    /// <summary>
    /// Returns all live entities. Enumeration order is unspecified; callers that need a
    /// deterministic order (e.g. for serialization) must sort explicitly.
    /// </summary>
    public IEnumerable<Entity> Entities => _entities;

    public ComponentStorage<T> GetStore<T>()
        where T : struct
    {
        if (_componentStores.TryGetValue(typeof(T), out var store))
            return (ComponentStorage<T>)store;
        var newStore = new ComponentStorage<T>();
        _componentStores[typeof(T)] = newStore;
        return newStore;
    }
}
