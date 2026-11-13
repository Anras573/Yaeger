using System.Collections;
using System.Runtime.CompilerServices;

namespace Yaeger.ECS;

/// <summary>
/// Sparse-set component store: components live packed in a dense <c>T[]</c> (with a parallel
/// dense <see cref="Entity"/>[]), and a paged sparse <c>int[]</c> indexed by <see cref="Entity.Id"/>
/// maps an entity to its dense slot. Iteration is a linear walk over contiguous memory,
/// <see cref="TryGet"/> is two array reads, and removal is swap-with-last.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mutation during iteration.</b> Overwriting an existing entity's component (the
/// <c>world.AddComponent(entity, updated)</c> write-back pattern, or writing through
/// <see cref="GetRef"/>) is always safe, including on the store being enumerated. Adding a new
/// entity to, or removing an entity from, the store being enumerated is a structural change: the
/// enumerator throws <see cref="InvalidOperationException"/> on its next step rather than
/// silently skipping or repeating entries (removal swaps the last element into the vacated slot).
/// Snapshot with <c>.ToList()</c> first if a loop needs to add or destroy.
/// </para>
/// <para>
/// <b>Iteration order</b> is dense order: insertion order, except that a removal moves the last
/// element into the freed slot. Callers needing a deterministic order must sort explicitly.
/// </para>
/// </remarks>
public class ComponentStorage<T> : IComponentStore
    where T : struct
{
    // Entity ids are monotonically increasing and never recycled, so a flat sparse array would
    // grow with every entity ever created. Paging allocates only the pages that hold ids actually
    // present in this store.
    private const int PageBits = 10;
    private const int PageSize = 1 << PageBits;
    private const int PageMask = PageSize - 1;

    private int[]?[] _pages = new int[]?[4];
    private Entity[] _entities = new Entity[4];
    private T[] _components = new T[4];
    private int _count;
    private int _version;

    public int Count => _count;

    /// <summary>Incremented by every structural change (a new entity added, or one removed).</summary>
    internal int Version => _version;

    internal Entity EntityAt(int denseIndex) => _entities[denseIndex];

    internal ref T ComponentAt(int denseIndex) => ref _components[denseIndex];

    /// <summary>Returns the dense slot for <paramref name="entity"/>, or -1 if it has none.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IndexOf(Entity entity)
    {
        var id = entity.Id;
        var pageIndex = id >> PageBits;
        var pages = _pages;
        if ((uint)pageIndex >= (uint)pages.Length)
            return -1;
        var page = pages[pageIndex];
        return page is null ? -1 : page[id & PageMask] - 1;
    }

    public bool Contains(Entity entity) => IndexOf(entity) >= 0;

    public void Add(Entity entity, T component)
    {
        var index = IndexOf(entity);
        if (index >= 0)
        {
            // Overwrite in place: not a structural change, so legal during iteration.
            _components[index] = component;
            return;
        }

        if (_count == _entities.Length)
        {
            var newCapacity = _count * 2;
            Array.Resize(ref _entities, newCapacity);
            Array.Resize(ref _components, newCapacity);
        }

        _entities[_count] = entity;
        _components[_count] = component;
        SetSparse(entity.Id, _count + 1);
        _count++;
        _version++;
    }

    public bool Remove(Entity entity)
    {
        var index = IndexOf(entity);
        if (index < 0)
            return false;

        var last = _count - 1;
        if (index != last)
        {
            var moved = _entities[last];
            _entities[index] = moved;
            _components[index] = _components[last];
            SetSparse(moved.Id, index + 1);
        }

        _entities[last] = default;
        _components[last] = default;
        SetSparse(entity.Id, 0);
        _count = last;
        _version++;
        return true;
    }

    public bool TryGet(Entity entity, out T component)
    {
        var index = IndexOf(entity);
        if (index >= 0)
        {
            component = _components[index];
            return true;
        }

        component = default;
        return false;
    }

    public T Get(Entity entity)
    {
        var index = IndexOf(entity);
        if (index < 0)
            throw new KeyNotFoundException($"Entity {entity.Id} has no {typeof(T).Name}.");
        return _components[index];
    }

    /// <summary>
    /// Returns a reference to <paramref name="entity"/>'s component so it can be modified in
    /// place instead of copy / modify / <c>AddComponent</c>. The reference is invalidated by any
    /// structural change to this store (adding or removing an entity); don't hold it across one.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The entity has no such component.</exception>
    public ref T GetRef(Entity entity)
    {
        var index = IndexOf(entity);
        if (index < 0)
            throw new KeyNotFoundException($"Entity {entity.Id} has no {typeof(T).Name}.");
        return ref _components[index];
    }

    /// <summary>
    /// Returns a reference to <paramref name="entity"/>'s component, or a null reference (test
    /// with <see cref="Unsafe.IsNullRef{T}"/>) if it has none.
    /// </summary>
    public ref T TryGetRef(Entity entity)
    {
        var index = IndexOf(entity);
        if (index < 0)
            return ref Unsafe.NullRef<T>();
        return ref _components[index];
    }

    private void SetSparse(int id, int value)
    {
        var pageIndex = id >> PageBits;
        if (pageIndex >= _pages.Length)
            Array.Resize(ref _pages, Math.Max(pageIndex + 1, _pages.Length * 2));
        var page = _pages[pageIndex];
        if (page is null)
        {
            if (value == 0)
                return;
            page = _pages[pageIndex] = new int[PageSize];
        }
        page[id & PageMask] = value;
    }

    /// <summary>
    /// Allocating <see cref="IEnumerable{T}"/> view over the (entity, component) pairs, for LINQ
    /// and snapshot (<c>.ToList()</c>) call sites. Prefer <c>foreach</c> over the store itself,
    /// which uses the non-allocating <see cref="GetEnumerator"/>.
    /// </summary>
    public IEnumerable<KeyValuePair<Entity, T>> All()
    {
        foreach (var pair in this)
            yield return pair;
    }

    /// <summary>
    /// Returns a non-allocating struct enumerator over the (entity, component) pairs, enabling
    /// <c>foreach</c> directly over the storage. Throws <see cref="InvalidOperationException"/>
    /// if an entity is added to or removed from the store mid-enumeration (see the class remarks).
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        private readonly ComponentStorage<T> _store;
        private readonly int _version;
        private int _index;

        internal Enumerator(ComponentStorage<T> store)
        {
            _store = store;
            _version = store._version;
            _index = -1;
        }

        public bool MoveNext()
        {
            if (_version != _store._version)
                throw new InvalidOperationException(
                    $"{typeof(T).Name} store was structurally modified during enumeration."
                );
            return ++_index < _store._count;
        }

        public readonly KeyValuePair<Entity, T> Current =>
            new(_store._entities[_index], _store._components[_index]);
    }
}
