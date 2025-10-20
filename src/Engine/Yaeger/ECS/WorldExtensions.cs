namespace Yaeger.ECS;

public static class WorldExtensions
{
    public static IEnumerable<(Entity, T1, T2)> Query<T1, T2>(this World world)
        where T1 : struct
        where T2 : struct
    {
        var store1 = world.GetStore<T1>();
        var store2 = world.GetStore<T2>();

        foreach ((Entity entity, T1 component1) in store1.All())
        {
            if (store2.TryGet(entity, out var component2))
            {
                yield return (entity, component1, component2);
            }
        }
    }

    public static IEnumerable<(Entity, T1, T2, T3)> Query<T1, T2, T3>(this World world)
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        var store1 = world.GetStore<T1>();
        var store2 = world.GetStore<T2>();
        var store3 = world.GetStore<T3>();

        foreach ((Entity entity, T1 component1) in store1.All())
        {
            if (store2.TryGet(entity, out var component2) && store3.TryGet(entity, out var component3))
            {
                yield return (entity, component1, component2, component3);
            }
        }
    }

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

        foreach ((Entity entity, T1 component1) in store1.All())
        {
            if (store2.TryGet(entity, out var component2) && store3.TryGet(entity, out var component3) && store4.TryGet(entity, out var component4))
            {
                yield return (entity, component1, component2, component3, component4);
            }
        }
    }
}