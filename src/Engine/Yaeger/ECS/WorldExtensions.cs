namespace Yaeger.ECS;

public static class WorldExtensions
{
    /// <summary>
    /// Queries entities with two components.
    /// Automatically iterates the smallest component store for better performance.
    /// Use the override method to force iteration of a specific component type.
    /// </summary>
    public static IEnumerable<(Entity, T1, T2)> Query<T1, T2>(this World world)
        where T1 : struct
        where T2 : struct
    {
        var store1 = world.GetStore<T1>();
        var store2 = world.GetStore<T2>();
        var minStoreIdx = store1.Count <= store2.Count ? 0 : 1;
        return Query2Helper<T1, T2>.Execute(minStoreIdx, store1, store2);
    }

    /// <summary>
    /// Queries entities with two components, forcing iteration of a specific component type.
    /// </summary>
    /// <param name="forceIndex">The index of the component type to iterate first (0 for T1, 1 for T2).</param>
    public static IEnumerable<(Entity, T1, T2)> Query<T1, T2>(this World world, int forceIndex)
        where T1 : struct
        where T2 : struct
    {
        if (forceIndex < 0 || forceIndex > 1)
            throw new ArgumentOutOfRangeException(nameof(forceIndex), "Must be 0 or 1");

        var store1 = world.GetStore<T1>();
        var store2 = world.GetStore<T2>();
        return Query2Helper<T1, T2>.Execute(forceIndex, store1, store2);
    }

    /// <summary>
    /// Queries entities with three components.
    /// Automatically iterates the smallest component store for better performance.
    /// Use the override method to force iteration of a specific component type.
    /// </summary>
    public static IEnumerable<(Entity, T1, T2, T3)> Query<T1, T2, T3>(this World world)
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        var store1 = world.GetStore<T1>();
        var store2 = world.GetStore<T2>();
        var store3 = world.GetStore<T3>();
        var minStoreIdx = MinStoreIndex(store1.Count, store2.Count, store3.Count);
        return Query3Helper<T1, T2, T3>.Execute(minStoreIdx, store1, store2, store3);
    }

    /// <summary>
    /// Queries entities with three components, forcing iteration of a specific component type.
    /// </summary>
    /// <param name="forceIndex">The index of the component type to iterate first (0 for T1, 1 for T2, 2 for T3).</param>
    public static IEnumerable<(Entity, T1, T2, T3)> Query<T1, T2, T3>(
        this World world,
        int forceIndex
    )
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        if (forceIndex < 0 || forceIndex > 2)
            throw new ArgumentOutOfRangeException(nameof(forceIndex), "Must be 0, 1, or 2");

        var store1 = world.GetStore<T1>();
        var store2 = world.GetStore<T2>();
        var store3 = world.GetStore<T3>();
        return Query3Helper<T1, T2, T3>.Execute(forceIndex, store1, store2, store3);
    }

    /// <summary>
    /// Queries entities with four components.
    /// Automatically iterates the smallest component store for better performance.
    /// Use the override method to force iteration of a specific component type.
    /// </summary>
    public static IEnumerable<(Entity, T1, T2, T3, T4)> Query<T1, T2, T3, T4>(this World world)
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
    {
        var store1 = world.GetStore<T1>();
        var store2 = world.GetStore<T2>();
        var store3 = world.GetStore<T3>();
        var store4 = world.GetStore<T4>();
        var minStoreIdx = MinStoreIndex(store1.Count, store2.Count, store3.Count, store4.Count);
        return Query4Helper<T1, T2, T3, T4>.Execute(minStoreIdx, store1, store2, store3, store4);
    }

    /// <summary>
    /// Queries entities with four components, forcing iteration of a specific component type.
    /// </summary>
    /// <param name="forceIndex">The index of the component type to iterate first (0 for T1, 1 for T2, 2 for T3, 3 for T4).</param>
    public static IEnumerable<(Entity, T1, T2, T3, T4)> Query<T1, T2, T3, T4>(
        this World world,
        int forceIndex
    )
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
    {
        if (forceIndex < 0 || forceIndex > 3)
            throw new ArgumentOutOfRangeException(nameof(forceIndex), "Must be 0, 1, 2, or 3");

        var store1 = world.GetStore<T1>();
        var store2 = world.GetStore<T2>();
        var store3 = world.GetStore<T3>();
        var store4 = world.GetStore<T4>();
        return Query4Helper<T1, T2, T3, T4>.Execute(forceIndex, store1, store2, store3, store4);
    }

    private static int MinStoreIndex(int count1, int count2, int count3)
    {
        if (count1 <= count2 && count1 <= count3)
            return 0;
        if (count2 <= count1 && count2 <= count3)
            return 1;
        return 2;
    }

    private static int MinStoreIndex(int count1, int count2, int count3, int count4)
    {
        if (count1 <= count2 && count1 <= count3 && count1 <= count4)
            return 0;
        if (count2 <= count1 && count2 <= count3 && count2 <= count4)
            return 1;
        if (count3 <= count1 && count3 <= count2 && count3 <= count4)
            return 2;
        return 3;
    }

    private static class Query2Helper<T1, T2>
        where T1 : struct
        where T2 : struct
    {
        internal static IEnumerable<(Entity, T1, T2)> Execute(
            int storeIndex,
            ComponentStorage<T1> store1,
            ComponentStorage<T2> store2
        )
        {
            return storeIndex == 0 ? IterateFirst(store1, store2) : IterateSecond(store1, store2);
        }

        private static IEnumerable<(Entity, T1, T2)> IterateFirst(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2
        )
        {
            foreach ((Entity entity, T1 c1) in s1.All())
            {
                if (s2.TryGet(entity, out var c2))
                {
                    yield return (entity, c1, c2);
                }
            }
        }

        private static IEnumerable<(Entity, T1, T2)> IterateSecond(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2
        )
        {
            foreach ((Entity entity, T2 c2) in s2.All())
            {
                if (s1.TryGet(entity, out var c1))
                {
                    yield return (entity, c1, c2);
                }
            }
        }
    }

    private static class Query3Helper<T1, T2, T3>
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        internal static IEnumerable<(Entity, T1, T2, T3)> Execute(
            int storeIndex,
            ComponentStorage<T1> store1,
            ComponentStorage<T2> store2,
            ComponentStorage<T3> store3
        )
        {
            return storeIndex switch
            {
                0 => IterateFirst(store1, store2, store3),
                1 => IterateSecond(store1, store2, store3),
                _ => IterateThird(store1, store2, store3),
            };
        }

        private static IEnumerable<(Entity, T1, T2, T3)> IterateFirst(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3
        )
        {
            foreach ((Entity entity, T1 c1) in s1.All())
            {
                if (s2.TryGet(entity, out var c2) && s3.TryGet(entity, out var c3))
                {
                    yield return (entity, c1, c2, c3);
                }
            }
        }

        private static IEnumerable<(Entity, T1, T2, T3)> IterateSecond(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3
        )
        {
            foreach ((Entity entity, T2 c2) in s2.All())
            {
                if (s1.TryGet(entity, out var c1) && s3.TryGet(entity, out var c3))
                {
                    yield return (entity, c1, c2, c3);
                }
            }
        }

        private static IEnumerable<(Entity, T1, T2, T3)> IterateThird(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3
        )
        {
            foreach ((Entity entity, T3 c3) in s3.All())
            {
                if (s1.TryGet(entity, out var c1) && s2.TryGet(entity, out var c2))
                {
                    yield return (entity, c1, c2, c3);
                }
            }
        }
    }

    private static class Query4Helper<T1, T2, T3, T4>
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
    {
        internal static IEnumerable<(Entity, T1, T2, T3, T4)> Execute(
            int storeIndex,
            ComponentStorage<T1> store1,
            ComponentStorage<T2> store2,
            ComponentStorage<T3> store3,
            ComponentStorage<T4> store4
        )
        {
            return storeIndex switch
            {
                0 => IterateFirst(store1, store2, store3, store4),
                1 => IterateSecond(store1, store2, store3, store4),
                2 => IterateThird(store1, store2, store3, store4),
                _ => IterateFourth(store1, store2, store3, store4),
            };
        }

        private static IEnumerable<(Entity, T1, T2, T3, T4)> IterateFirst(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3,
            ComponentStorage<T4> s4
        )
        {
            foreach ((Entity entity, T1 c1) in s1.All())
            {
                if (
                    s2.TryGet(entity, out var c2)
                    && s3.TryGet(entity, out var c3)
                    && s4.TryGet(entity, out var c4)
                )
                {
                    yield return (entity, c1, c2, c3, c4);
                }
            }
        }

        private static IEnumerable<(Entity, T1, T2, T3, T4)> IterateSecond(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3,
            ComponentStorage<T4> s4
        )
        {
            foreach ((Entity entity, T2 c2) in s2.All())
            {
                if (
                    s1.TryGet(entity, out var c1)
                    && s3.TryGet(entity, out var c3)
                    && s4.TryGet(entity, out var c4)
                )
                {
                    yield return (entity, c1, c2, c3, c4);
                }
            }
        }

        private static IEnumerable<(Entity, T1, T2, T3, T4)> IterateThird(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3,
            ComponentStorage<T4> s4
        )
        {
            foreach ((Entity entity, T3 c3) in s3.All())
            {
                if (
                    s1.TryGet(entity, out var c1)
                    && s2.TryGet(entity, out var c2)
                    && s4.TryGet(entity, out var c4)
                )
                {
                    yield return (entity, c1, c2, c3, c4);
                }
            }
        }

        private static IEnumerable<(Entity, T1, T2, T3, T4)> IterateFourth(
            ComponentStorage<T1> s1,
            ComponentStorage<T2> s2,
            ComponentStorage<T3> s3,
            ComponentStorage<T4> s4
        )
        {
            foreach ((Entity entity, T4 c4) in s4.All())
            {
                if (
                    s1.TryGet(entity, out var c1)
                    && s2.TryGet(entity, out var c2)
                    && s3.TryGet(entity, out var c3)
                )
                {
                    yield return (entity, c1, c2, c3, c4);
                }
            }
        }
    }
}
