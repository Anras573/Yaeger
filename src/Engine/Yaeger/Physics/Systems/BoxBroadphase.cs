using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;

namespace Yaeger.Physics.Systems;

/// <summary>
/// A persistent spatial index over every <see cref="BoxCollider2D"/> + <see cref="Transform2D"/>
/// entity in a world, shared by the 2D physics consumers that need "which colliders are near
/// this region?" — the tunneling sweep in <see cref="MovementSystem"/>, the move-and-slide in
/// <see cref="CharacterControllerSystem"/>, and the pair generation in
/// <see cref="CollisionDetectionSystem"/> — instead of each walking every collider in the world.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Sync"/> makes the index match the world: it makes one cheap pass over the box
/// colliders, but only touches the hash for a collider that is new, gone, or whose world AABB
/// actually changed. Static geometry (hand-placed platforms, tilemap-generated colliders) is
/// therefore inserted once and never rehashed, while movers are re-inserted only when they move.
/// An entry keeps a copy of the collider data and centre as of the last sync, so a query sees one
/// consistent snapshot regardless of what moves afterwards.
/// </para>
/// <para>
/// Each entry also gets an <see cref="Entry.Order"/> — its position in the box store's iteration
/// order at the last sync — so consumers that need a deterministic order (manifolds, move-and-slide
/// candidates) can sort query results by it and get exactly the order a full
/// <c>world.Query</c> would have produced.
/// </para>
/// </remarks>
internal sealed class BoxBroadphase
{
    internal sealed class Entry
    {
        public Entity Entity;
        public BoxCollider2D Collider;
        public Vector2 Center;
        public Vector2 Half;

        /// <summary>The entity's <see cref="Transform2D"/> position at the last sync.</summary>
        public Vector2 Position;

        /// <summary>The position at the sync before that; only meaningful when <see cref="HasPrevious"/>.</summary>
        public Vector2 PreviousPosition;
        public bool HasPrevious;

        /// <summary>Has a <see cref="RigidBody2D"/> of type <see cref="BodyType.Static"/>.</summary>
        public bool IsStaticBody;

        /// <summary>Has a <see cref="RigidBody2D"/> of type <see cref="BodyType.Dynamic"/>.</summary>
        public bool IsDynamicBody;

        /// <summary>Index in the box store's iteration order at the last sync.</summary>
        public int Order;

        internal int Id;
        internal int SyncStamp;

        // Where the entry currently lives: in the hash, in the oversize list, or nowhere
        // (non-finite bounds).
        internal Residence Where;
        internal Vector2 HashedMin;
        internal Vector2 HashedMax;
    }

    internal enum Residence
    {
        None,
        Hash,
        Oversize,
    }

    private readonly World _world;
    private readonly SpatialHash _hash;
    private readonly Dictionary<Entity, int> _idByEntity = new();
    private readonly List<Entry?> _entries = [];
    private readonly Stack<int> _freeIds = new();
    private readonly List<int> _ordered = [];

    // Colliders too large for the hash (only when it isn't set to throw); scanned linearly.
    private readonly List<int> _oversize = [];

    private readonly List<int> _hashHits = [];
    private readonly List<long> _sortKeys = [];
    private int[] _visited = new int[16];
    private int _queryStamp;
    private int _syncStamp;

    internal BoxBroadphase(World world, float cellSize, bool throwOnOversize)
    {
        if (cellSize <= 0 || !float.IsFinite(cellSize))
            throw new ArgumentOutOfRangeException(
                nameof(cellSize),
                cellSize,
                "Cell size must be a positive finite value."
            );

        _world = world;
        CellSize = cellSize;
        _hash = new SpatialHash(cellSize, throwOnOversize);
    }

    internal float CellSize { get; }

    /// <summary>
    /// Incremented whenever a <see cref="BodyType.Static"/>-body entry is added, removed, moved,
    /// resized or reclassified. Lets a consumer cache anything derived purely from the static
    /// geometry (e.g. which static colliders overlap each other) and recompute only on change.
    /// </summary>
    internal int StaticRevision { get; private set; }

    /// <summary>Entry ids in box-store iteration order as of the last <see cref="Sync"/>.</summary>
    internal IReadOnlyList<int> Ordered => _ordered;

    internal Entry Get(int id) => _entries[id]!;

    /// <summary>The entry for <paramref name="entity"/> as of the last sync, or <c>null</c>.</summary>
    internal Entry? Find(Entity entity) =>
        _idByEntity.TryGetValue(entity, out var id) ? _entries[id] : null;

    /// <summary>
    /// Brings the index in line with the world: adds new box colliders, drops destroyed ones, and
    /// re-hashes only colliders whose world AABB changed. Allocation-free once the set of
    /// colliders is steady.
    /// </summary>
    internal void Sync()
    {
        _syncStamp++;
        _ordered.Clear();
        var seen = 0;

        foreach (
            (Entity entity, BoxCollider2D collider, Transform2D transform) in _world.Query<
                BoxCollider2D,
                Transform2D
            >()
        )
        {
            Entry entry;
            var isNew = !_idByEntity.TryGetValue(entity, out var id);
            if (isNew)
            {
                id = AllocateId();
                entry = new Entry { Entity = entity, Id = id };
                _entries[id] = entry;
                _idByEntity[entity] = id;
            }
            else
            {
                entry = _entries[id]!;
            }

            var center = transform.Position + collider.Offset;
            var half = collider.HalfSize;
            var changed = isNew || center != entry.Center || half != entry.Half;

            if (changed)
                Unhash(entry);

            var wasStatic = !isNew && entry.IsStaticBody;
            entry.PreviousPosition = entry.Position;
            entry.HasPrevious = !isNew;
            entry.Position = transform.Position;
            entry.Collider = collider;
            entry.Center = center;
            entry.Half = half;

            var hasBody = _world.TryGetComponent<RigidBody2D>(entity, out var body);
            entry.IsStaticBody = hasBody && body.Type == BodyType.Static;
            entry.IsDynamicBody = hasBody && body.Type == BodyType.Dynamic;

            if ((changed || wasStatic != entry.IsStaticBody) && (wasStatic || entry.IsStaticBody))
                StaticRevision++;

            entry.Order = _ordered.Count;
            entry.SyncStamp = _syncStamp;
            _ordered.Add(id);
            seen++;

            if (changed)
                Hash(entry);
        }

        if (seen != _idByEntity.Count)
            RemoveUnseen();
    }

    private int AllocateId()
    {
        if (_freeIds.Count > 0)
            return _freeIds.Pop();

        _entries.Add(null);
        if (_entries.Count > _visited.Length)
            Array.Resize(ref _visited, _visited.Length * 2);
        return _entries.Count - 1;
    }

    private void RemoveUnseen()
    {
        // Entries not touched by this sync belong to destroyed entities (or ones that lost
        // their collider/transform). Walk the ids rather than the dictionary so we can edit
        // both while iterating.
        for (var id = 0; id < _entries.Count; id++)
        {
            var entry = _entries[id];
            if (entry is null || entry.SyncStamp == _syncStamp)
                continue;

            if (entry.IsStaticBody)
                StaticRevision++;
            Unhash(entry);
            _idByEntity.Remove(entry.Entity);
            _entries[id] = null;
            _freeIds.Push(id);
        }
    }

    private void Hash(Entry entry)
    {
        var min = entry.Center - entry.Half;
        var max = entry.Center + entry.Half;

        if (_hash.Insert(entry.Id, min, max))
        {
            entry.Where = Residence.Hash;
            entry.HashedMin = min;
            entry.HashedMax = max;
        }
        else if (
            float.IsFinite(min.X)
            && float.IsFinite(min.Y)
            && float.IsFinite(max.X)
            && float.IsFinite(max.Y)
        )
        {
            // Finite but too large (or beyond the int cell range) for the hash: keep it
            // reachable through a linear scan rather than silently dropping it.
            entry.Where = Residence.Oversize;
            _oversize.Add(entry.Id);
        }
        else
        {
            entry.Where = Residence.None;
        }
    }

    private void Unhash(Entry entry)
    {
        switch (entry.Where)
        {
            case Residence.Hash:
                _hash.Remove(entry.Id, entry.HashedMin, entry.HashedMax);
                break;
            case Residence.Oversize:
                _oversize.Remove(entry.Id);
                break;
        }
        entry.Where = Residence.None;
    }

    /// <summary>
    /// Finds every entry whose AABB overlaps (or touches) the region <paramref name="min"/>–
    /// <paramref name="max"/> as of the last <see cref="Sync"/>, appending its id to
    /// <paramref name="results"/> exactly once, in no particular order. A region too large for the
    /// hash degrades to a scan of all entries rather than failing; a NaN region matches nothing.
    /// </summary>
    internal void Query(Vector2 min, Vector2 max, List<int> results)
    {
        // A NaN region overlaps nothing. It must be rejected here: it can't be hashed, so it
        // would reach the scan below, where every comparison against NaN is false and every
        // entry would pass. (An infinite region is fine: it scans and compares correctly.)
        if (float.IsNaN(min.X) || float.IsNaN(min.Y) || float.IsNaN(max.X) || float.IsNaN(max.Y))
            return;

        _queryStamp++;
        _hashHits.Clear();

        if (_hash.Query(min, max, _hashHits))
        {
            foreach (var id in _hashHits)
                TryAdd(id, min, max, results);
            foreach (var id in _oversize)
                TryAdd(id, min, max, results);
        }
        else
        {
            foreach (var id in _ordered)
                TryAdd(id, min, max, results);
        }
    }

    /// <summary>
    /// Sorts <paramref name="ids"/> (as returned by <see cref="Query"/>) into <see cref="Entry.Order"/>
    /// order — the order a full <c>world.Query</c> over the box store would visit them in.
    /// </summary>
    internal void SortByOrder(List<int> ids)
    {
        if (ids.Count < 2)
            return;

        _sortKeys.Clear();
        foreach (var id in ids)
            _sortKeys.Add(((long)_entries[id]!.Order << 32) | (uint)id);
        _sortKeys.Sort();

        for (var i = 0; i < ids.Count; i++)
            ids[i] = (int)(_sortKeys[i] & 0xFFFFFFFF);
    }

    private void TryAdd(int id, Vector2 min, Vector2 max, List<int> results)
    {
        if (_visited[id] == _queryStamp)
            return;
        _visited[id] = _queryStamp;

        var entry = _entries[id]!;
        if (entry.Where == Residence.None)
            return;

        var entryMin = entry.Center - entry.Half;
        var entryMax = entry.Center + entry.Half;
        if (entryMax.X < min.X || entryMin.X > max.X || entryMax.Y < min.Y || entryMin.Y > max.Y)
            return;

        results.Add(id);
    }
}
