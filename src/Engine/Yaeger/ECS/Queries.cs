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
        private readonly int _version;
        private int _index;

        internal Enumerator(ComponentStorage<T1> s1, ComponentStorage<T2> s2, int drive)
        {
            _s1 = s1;
            _s2 = s2;
            _drive = drive;
            _index = -1;
            (_count, _version) = drive switch
            {
                0 => (s1.Count, s1.Version),
                _ => (s2.Count, s2.Version),
            };
            Current = default;
        }

        public (Entity, T1, T2) Current { get; private set; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            switch (_drive)
            {
                case 0:
                    while (++_index < _count)
                    {
                        if (_s1.Version != _version)
                            ThrowModified();
                        var entity = _s1.EntityAt(_index);
                        if (!_s2.TryGet(entity, out var c2))
                            continue;
                        Current = (entity, _s1.ComponentAt(_index), c2);
                        return true;
                    }
                    return false;
                default:
                    while (++_index < _count)
                    {
                        if (_s2.Version != _version)
                            ThrowModified();
                        var entity = _s2.EntityAt(_index);
                        if (!_s1.TryGet(entity, out var c1))
                            continue;
                        Current = (entity, c1, _s2.ComponentAt(_index));
                        return true;
                    }
                    return false;
            }
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() { }

        private static void ThrowModified() =>
            throw new InvalidOperationException(
                "The driving component store was structurally modified during enumeration. "
                    + "Snapshot the query with ToList() before adding or destroying entities."
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
        private readonly int _version;
        private int _index;

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
            (_count, _version) = drive switch
            {
                0 => (s1.Count, s1.Version),
                1 => (s2.Count, s2.Version),
                _ => (s3.Count, s3.Version),
            };
            Current = default;
        }

        public (Entity, T1, T2, T3) Current { get; private set; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            switch (_drive)
            {
                case 0:
                    while (++_index < _count)
                    {
                        if (_s1.Version != _version)
                            ThrowModified();
                        var entity = _s1.EntityAt(_index);
                        if (!_s2.TryGet(entity, out var c2))
                            continue;
                        if (!_s3.TryGet(entity, out var c3))
                            continue;
                        Current = (entity, _s1.ComponentAt(_index), c2, c3);
                        return true;
                    }
                    return false;
                case 1:
                    while (++_index < _count)
                    {
                        if (_s2.Version != _version)
                            ThrowModified();
                        var entity = _s2.EntityAt(_index);
                        if (!_s1.TryGet(entity, out var c1))
                            continue;
                        if (!_s3.TryGet(entity, out var c3))
                            continue;
                        Current = (entity, c1, _s2.ComponentAt(_index), c3);
                        return true;
                    }
                    return false;
                default:
                    while (++_index < _count)
                    {
                        if (_s3.Version != _version)
                            ThrowModified();
                        var entity = _s3.EntityAt(_index);
                        if (!_s1.TryGet(entity, out var c1))
                            continue;
                        if (!_s2.TryGet(entity, out var c2))
                            continue;
                        Current = (entity, c1, c2, _s3.ComponentAt(_index));
                        return true;
                    }
                    return false;
            }
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() { }

        private static void ThrowModified() =>
            throw new InvalidOperationException(
                "The driving component store was structurally modified during enumeration. "
                    + "Snapshot the query with ToList() before adding or destroying entities."
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
        private readonly int _version;
        private int _index;

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
            (_count, _version) = drive switch
            {
                0 => (s1.Count, s1.Version),
                1 => (s2.Count, s2.Version),
                2 => (s3.Count, s3.Version),
                _ => (s4.Count, s4.Version),
            };
            Current = default;
        }

        public (Entity, T1, T2, T3, T4) Current { get; private set; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            switch (_drive)
            {
                case 0:
                    while (++_index < _count)
                    {
                        if (_s1.Version != _version)
                            ThrowModified();
                        var entity = _s1.EntityAt(_index);
                        if (!_s2.TryGet(entity, out var c2))
                            continue;
                        if (!_s3.TryGet(entity, out var c3))
                            continue;
                        if (!_s4.TryGet(entity, out var c4))
                            continue;
                        Current = (entity, _s1.ComponentAt(_index), c2, c3, c4);
                        return true;
                    }
                    return false;
                case 1:
                    while (++_index < _count)
                    {
                        if (_s2.Version != _version)
                            ThrowModified();
                        var entity = _s2.EntityAt(_index);
                        if (!_s1.TryGet(entity, out var c1))
                            continue;
                        if (!_s3.TryGet(entity, out var c3))
                            continue;
                        if (!_s4.TryGet(entity, out var c4))
                            continue;
                        Current = (entity, c1, _s2.ComponentAt(_index), c3, c4);
                        return true;
                    }
                    return false;
                case 2:
                    while (++_index < _count)
                    {
                        if (_s3.Version != _version)
                            ThrowModified();
                        var entity = _s3.EntityAt(_index);
                        if (!_s1.TryGet(entity, out var c1))
                            continue;
                        if (!_s2.TryGet(entity, out var c2))
                            continue;
                        if (!_s4.TryGet(entity, out var c4))
                            continue;
                        Current = (entity, c1, c2, _s3.ComponentAt(_index), c4);
                        return true;
                    }
                    return false;
                default:
                    while (++_index < _count)
                    {
                        if (_s4.Version != _version)
                            ThrowModified();
                        var entity = _s4.EntityAt(_index);
                        if (!_s1.TryGet(entity, out var c1))
                            continue;
                        if (!_s2.TryGet(entity, out var c2))
                            continue;
                        if (!_s3.TryGet(entity, out var c3))
                            continue;
                        Current = (entity, c1, c2, c3, _s4.ComponentAt(_index));
                        return true;
                    }
                    return false;
            }
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() { }

        private static void ThrowModified() =>
            throw new InvalidOperationException(
                "The driving component store was structurally modified during enumeration. "
                    + "Snapshot the query with ToList() before adding or destroying entities."
            );
    }
}
