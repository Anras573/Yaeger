using System.Collections;

namespace Yaeger.ECS;

/// <summary>
/// Allocation-free result of <c>Query&lt;T1, T2&gt;()</c>. <c>foreach</c> binds to the struct
/// <see cref="GetEnumerator"/>; it also implements <see cref="IEnumerable{T}"/> (boxing) so
/// LINQ and <c>.ToList()</c> snapshots keep working.
/// </summary>
public readonly struct Query2<T1, T2> : IEnumerable<(Entity, T1, T2)>
    where T1 : struct
    where T2 : struct
{
    private readonly ComponentStorage<T1> _s1;
    private readonly ComponentStorage<T2> _s2;
    private readonly int _drive;

    internal Query2(ComponentStorage<T1> s1, ComponentStorage<T2> s2, int drive)
    {
        _s1 = s1;
        _s2 = s2;
        _drive = drive;
    }

    public Enumerator GetEnumerator() => new(_s1, _s2, _drive);

    IEnumerator<(Entity, T1, T2)> IEnumerable<(Entity, T1, T2)>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<(Entity, T1, T2)>
    {
        private readonly ComponentStorage<T1> _s1;
        private readonly ComponentStorage<T2> _s2;
        private readonly int _drive;
        private readonly int _count;
        private readonly int _v1;
        private readonly int _v2;
        private int _index;

        // The current match, held as the entity plus its dense slot in each store rather than as
        // a materialised tuple: Current then copies each component exactly once, straight out of
        // its store, instead of once into a cached tuple here and again out to the caller.
        private Entity _entity;
        private int _i1;
        private int _i2;

        internal Enumerator(ComponentStorage<T1> s1, ComponentStorage<T2> s2, int drive)
        {
            _s1 = s1;
            _s2 = s2;
            _drive = drive;
            _index = -1;
            _count = drive switch
            {
                0 => s1.Count,
                _ => s2.Count,
            };
            _v1 = s1.Version;
            _v2 = s2.Version;
            _entity = default;
            _i1 = 0;
            _i2 = 0;
        }

        public readonly (Entity, T1, T2) Current =>
            (_entity, _s1.ComponentAt(_i1), _s2.ComponentAt(_i2));

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            // Checked on every step, including the one that ends the loop, and against every
            // store in the query (not just the driving one): which store drives depends on
            // relative store sizes, so a narrower check would make the same loop throw or not
            // depending on the data.
            if (_s1.Version != _v1 || _s2.Version != _v2)
                ThrowModified();

            switch (_drive)
            {
                case 0:
                    while (++_index < _count)
                    {
                        var entity = _s1.EntityAt(_index);
                        var i2 = _s2.IndexOf(entity);
                        if (i2 < 0)
                            continue;
                        _entity = entity;
                        _i1 = _index;
                        _i2 = i2;
                        return true;
                    }
                    return false;
                default:
                    while (++_index < _count)
                    {
                        var entity = _s2.EntityAt(_index);
                        var i1 = _s1.IndexOf(entity);
                        if (i1 < 0)
                            continue;
                        _entity = entity;
                        _i1 = i1;
                        _i2 = _index;
                        return true;
                    }
                    return false;
            }
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() { }

        private static void ThrowModified() =>
            throw new InvalidOperationException(
                "A component store in this query was structurally modified during enumeration. "
                    + "Snapshot the query with ToList() before adding or removing its components or destroying entities."
            );
    }
}

/// <summary>
/// Allocation-free result of <c>Query&lt;T1, T2, T3&gt;()</c>. <c>foreach</c> binds to the struct
/// <see cref="GetEnumerator"/>; it also implements <see cref="IEnumerable{T}"/> (boxing) so
/// LINQ and <c>.ToList()</c> snapshots keep working.
/// </summary>
public readonly struct Query3<T1, T2, T3> : IEnumerable<(Entity, T1, T2, T3)>
    where T1 : struct
    where T2 : struct
    where T3 : struct
{
    private readonly ComponentStorage<T1> _s1;
    private readonly ComponentStorage<T2> _s2;
    private readonly ComponentStorage<T3> _s3;
    private readonly int _drive;

    internal Query3(
        ComponentStorage<T1> s1,
        ComponentStorage<T2> s2,
        ComponentStorage<T3> s3,
        int drive
    )
    {
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
        _drive = drive;
    }

    public Enumerator GetEnumerator() => new(_s1, _s2, _s3, _drive);

    IEnumerator<(Entity, T1, T2, T3)> IEnumerable<(Entity, T1, T2, T3)>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<(Entity, T1, T2, T3)>
    {
        private readonly ComponentStorage<T1> _s1;
        private readonly ComponentStorage<T2> _s2;
        private readonly ComponentStorage<T3> _s3;
        private readonly int _drive;
        private readonly int _count;
        private readonly int _v1;
        private readonly int _v2;
        private readonly int _v3;
        private int _index;

        // The current match, held as the entity plus its dense slot in each store rather than as
        // a materialised tuple: Current then copies each component exactly once, straight out of
        // its store, instead of once into a cached tuple here and again out to the caller.
        private Entity _entity;
        private int _i1;
        private int _i2;
        private int _i3;

        internal Enumerator(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3,
            int drive
        )
        {
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _drive = drive;
            _index = -1;
            _count = drive switch
            {
                0 => s1.Count,
                1 => s2.Count,
                _ => s3.Count,
            };
            _v1 = s1.Version;
            _v2 = s2.Version;
            _v3 = s3.Version;
            _entity = default;
            _i1 = 0;
            _i2 = 0;
            _i3 = 0;
        }

        public readonly (Entity, T1, T2, T3) Current =>
            (_entity, _s1.ComponentAt(_i1), _s2.ComponentAt(_i2), _s3.ComponentAt(_i3));

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            // Checked on every step, including the one that ends the loop, and against every
            // store in the query (not just the driving one): which store drives depends on
            // relative store sizes, so a narrower check would make the same loop throw or not
            // depending on the data.
            if (_s1.Version != _v1 || _s2.Version != _v2 || _s3.Version != _v3)
                ThrowModified();

            switch (_drive)
            {
                case 0:
                    while (++_index < _count)
                    {
                        var entity = _s1.EntityAt(_index);
                        var i2 = _s2.IndexOf(entity);
                        if (i2 < 0)
                            continue;
                        var i3 = _s3.IndexOf(entity);
                        if (i3 < 0)
                            continue;
                        _entity = entity;
                        _i1 = _index;
                        _i2 = i2;
                        _i3 = i3;
                        return true;
                    }
                    return false;
                case 1:
                    while (++_index < _count)
                    {
                        var entity = _s2.EntityAt(_index);
                        var i1 = _s1.IndexOf(entity);
                        if (i1 < 0)
                            continue;
                        var i3 = _s3.IndexOf(entity);
                        if (i3 < 0)
                            continue;
                        _entity = entity;
                        _i1 = i1;
                        _i2 = _index;
                        _i3 = i3;
                        return true;
                    }
                    return false;
                default:
                    while (++_index < _count)
                    {
                        var entity = _s3.EntityAt(_index);
                        var i1 = _s1.IndexOf(entity);
                        if (i1 < 0)
                            continue;
                        var i2 = _s2.IndexOf(entity);
                        if (i2 < 0)
                            continue;
                        _entity = entity;
                        _i1 = i1;
                        _i2 = i2;
                        _i3 = _index;
                        return true;
                    }
                    return false;
            }
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() { }

        private static void ThrowModified() =>
            throw new InvalidOperationException(
                "A component store in this query was structurally modified during enumeration. "
                    + "Snapshot the query with ToList() before adding or removing its components or destroying entities."
            );
    }
}

/// <summary>
/// Allocation-free result of <c>Query&lt;T1, T2, T3, T4&gt;()</c>. <c>foreach</c> binds to the struct
/// <see cref="GetEnumerator"/>; it also implements <see cref="IEnumerable{T}"/> (boxing) so
/// LINQ and <c>.ToList()</c> snapshots keep working.
/// </summary>
public readonly struct Query4<T1, T2, T3, T4> : IEnumerable<(Entity, T1, T2, T3, T4)>
    where T1 : struct
    where T2 : struct
    where T3 : struct
    where T4 : struct
{
    private readonly ComponentStorage<T1> _s1;
    private readonly ComponentStorage<T2> _s2;
    private readonly ComponentStorage<T3> _s3;
    private readonly ComponentStorage<T4> _s4;
    private readonly int _drive;

    internal Query4(
        ComponentStorage<T1> s1,
        ComponentStorage<T2> s2,
        ComponentStorage<T3> s3,
        ComponentStorage<T4> s4,
        int drive
    )
    {
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
        _s4 = s4;
        _drive = drive;
    }

    public Enumerator GetEnumerator() => new(_s1, _s2, _s3, _s4, _drive);

    IEnumerator<(Entity, T1, T2, T3, T4)> IEnumerable<(Entity, T1, T2, T3, T4)>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<(Entity, T1, T2, T3, T4)>
    {
        private readonly ComponentStorage<T1> _s1;
        private readonly ComponentStorage<T2> _s2;
        private readonly ComponentStorage<T3> _s3;
        private readonly ComponentStorage<T4> _s4;
        private readonly int _drive;
        private readonly int _count;
        private readonly int _v1;
        private readonly int _v2;
        private readonly int _v3;
        private readonly int _v4;
        private int _index;

        // The current match, held as the entity plus its dense slot in each store rather than as
        // a materialised tuple: Current then copies each component exactly once, straight out of
        // its store, instead of once into a cached tuple here and again out to the caller.
        private Entity _entity;
        private int _i1;
        private int _i2;
        private int _i3;
        private int _i4;

        internal Enumerator(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3,
            ComponentStorage<T4> s4,
            int drive
        )
        {
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _s4 = s4;
            _drive = drive;
            _index = -1;
            _count = drive switch
            {
                0 => s1.Count,
                1 => s2.Count,
                2 => s3.Count,
                _ => s4.Count,
            };
            _v1 = s1.Version;
            _v2 = s2.Version;
            _v3 = s3.Version;
            _v4 = s4.Version;
            _entity = default;
            _i1 = 0;
            _i2 = 0;
            _i3 = 0;
            _i4 = 0;
        }

        public readonly (Entity, T1, T2, T3, T4) Current =>
            (
                _entity,
                _s1.ComponentAt(_i1),
                _s2.ComponentAt(_i2),
                _s3.ComponentAt(_i3),
                _s4.ComponentAt(_i4)
            );

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            // Checked on every step, including the one that ends the loop, and against every
            // store in the query (not just the driving one): which store drives depends on
            // relative store sizes, so a narrower check would make the same loop throw or not
            // depending on the data.
            if (
                _s1.Version != _v1
                || _s2.Version != _v2
                || _s3.Version != _v3
                || _s4.Version != _v4
            )
                ThrowModified();

            switch (_drive)
            {
                case 0:
                    while (++_index < _count)
                    {
                        var entity = _s1.EntityAt(_index);
                        var i2 = _s2.IndexOf(entity);
                        if (i2 < 0)
                            continue;
                        var i3 = _s3.IndexOf(entity);
                        if (i3 < 0)
                            continue;
                        var i4 = _s4.IndexOf(entity);
                        if (i4 < 0)
                            continue;
                        _entity = entity;
                        _i1 = _index;
                        _i2 = i2;
                        _i3 = i3;
                        _i4 = i4;
                        return true;
                    }
                    return false;
                case 1:
                    while (++_index < _count)
                    {
                        var entity = _s2.EntityAt(_index);
                        var i1 = _s1.IndexOf(entity);
                        if (i1 < 0)
                            continue;
                        var i3 = _s3.IndexOf(entity);
                        if (i3 < 0)
                            continue;
                        var i4 = _s4.IndexOf(entity);
                        if (i4 < 0)
                            continue;
                        _entity = entity;
                        _i1 = i1;
                        _i2 = _index;
                        _i3 = i3;
                        _i4 = i4;
                        return true;
                    }
                    return false;
                case 2:
                    while (++_index < _count)
                    {
                        var entity = _s3.EntityAt(_index);
                        var i1 = _s1.IndexOf(entity);
                        if (i1 < 0)
                            continue;
                        var i2 = _s2.IndexOf(entity);
                        if (i2 < 0)
                            continue;
                        var i4 = _s4.IndexOf(entity);
                        if (i4 < 0)
                            continue;
                        _entity = entity;
                        _i1 = i1;
                        _i2 = i2;
                        _i3 = _index;
                        _i4 = i4;
                        return true;
                    }
                    return false;
                default:
                    while (++_index < _count)
                    {
                        var entity = _s4.EntityAt(_index);
                        var i1 = _s1.IndexOf(entity);
                        if (i1 < 0)
                            continue;
                        var i2 = _s2.IndexOf(entity);
                        if (i2 < 0)
                            continue;
                        var i3 = _s3.IndexOf(entity);
                        if (i3 < 0)
                            continue;
                        _entity = entity;
                        _i1 = i1;
                        _i2 = i2;
                        _i3 = i3;
                        _i4 = _index;
                        return true;
                    }
                    return false;
            }
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() { }

        private static void ThrowModified() =>
            throw new InvalidOperationException(
                "A component store in this query was structurally modified during enumeration. "
                    + "Snapshot the query with ToList() before adding or removing its components or destroying entities."
            );
    }
}
