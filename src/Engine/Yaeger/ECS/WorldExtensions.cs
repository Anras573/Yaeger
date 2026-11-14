namespace Yaeger.ECS;

public static class WorldExtensions
{
    /// <summary>
    /// Queries entities with 2 components. Allocation-free: iterates the smallest component
    /// store and probes the rest. Use the overload taking <c>forceIndex</c> to pin which one drives.
    /// </summary>
    public static Query2<T1, T2> Query<T1, T2>(this World world)
        where T1 : struct
        where T2 : struct
    {
        var s1 = world.GetStore<T1>();
        var s2 = world.GetStore<T2>();
        return new Query2<T1, T2>(s1, s2, s1.Count <= s2.Count ? 0 : 1);
    }

    /// <summary>
    /// Queries entities with 2 components, forcing iteration of a specific component type.
    /// </summary>
    /// <param name="forceIndex">Index of the component type to iterate (0 for T1, 1 for T2, ...).</param>
    public static Query2<T1, T2> Query<T1, T2>(this World world, int forceIndex)
        where T1 : struct
        where T2 : struct
    {
        if (forceIndex < 0 || forceIndex > 1)
            throw new ArgumentOutOfRangeException(nameof(forceIndex), "Must be between 0 and 1");

        var s1 = world.GetStore<T1>();
        var s2 = world.GetStore<T2>();
        return new Query2<T1, T2>(s1, s2, forceIndex);
    }

    /// <summary>
    /// Queries entities with 3 components. Allocation-free: iterates the smallest component
    /// store and probes the rest. Use the overload taking <c>forceIndex</c> to pin which one drives.
    /// </summary>
    public static Query3<T1, T2, T3> Query<T1, T2, T3>(this World world)
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        var s1 = world.GetStore<T1>();
        var s2 = world.GetStore<T2>();
        var s3 = world.GetStore<T3>();
        return new Query3<T1, T2, T3>(s1, s2, s3, MinStoreIndex(s1.Count, s2.Count, s3.Count));
    }

    /// <summary>
    /// Queries entities with 3 components, forcing iteration of a specific component type.
    /// </summary>
    /// <param name="forceIndex">Index of the component type to iterate (0 for T1, 1 for T2, ...).</param>
    public static Query3<T1, T2, T3> Query<T1, T2, T3>(this World world, int forceIndex)
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        if (forceIndex < 0 || forceIndex > 2)
            throw new ArgumentOutOfRangeException(nameof(forceIndex), "Must be between 0 and 2");

        var s1 = world.GetStore<T1>();
        var s2 = world.GetStore<T2>();
        var s3 = world.GetStore<T3>();
        return new Query3<T1, T2, T3>(s1, s2, s3, forceIndex);
    }

    /// <summary>
    /// Queries entities with 4 components. Allocation-free: iterates the smallest component
    /// store and probes the rest. Use the overload taking <c>forceIndex</c> to pin which one drives.
    /// </summary>
    public static Query4<T1, T2, T3, T4> Query<T1, T2, T3, T4>(this World world)
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
    {
        var s1 = world.GetStore<T1>();
        var s2 = world.GetStore<T2>();
        var s3 = world.GetStore<T3>();
        var s4 = world.GetStore<T4>();
        return new Query4<T1, T2, T3, T4>(
            s1,
            s2,
            s3,
            s4,
            MinStoreIndex(s1.Count, s2.Count, s3.Count, s4.Count)
        );
    }

    /// <summary>
    /// Queries entities with 4 components, forcing iteration of a specific component type.
    /// </summary>
    /// <param name="forceIndex">Index of the component type to iterate (0 for T1, 1 for T2, ...).</param>
    public static Query4<T1, T2, T3, T4> Query<T1, T2, T3, T4>(this World world, int forceIndex)
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
    {
        if (forceIndex < 0 || forceIndex > 3)
            throw new ArgumentOutOfRangeException(nameof(forceIndex), "Must be between 0 and 3");

        var s1 = world.GetStore<T1>();
        var s2 = world.GetStore<T2>();
        var s3 = world.GetStore<T3>();
        var s4 = world.GetStore<T4>();
        return new Query4<T1, T2, T3, T4>(s1, s2, s3, s4, forceIndex);
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
}
