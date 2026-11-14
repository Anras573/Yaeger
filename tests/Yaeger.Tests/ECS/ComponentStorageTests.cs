using Yaeger.ECS;

namespace Yaeger.Tests.ECS;

public class ComponentStorageTests
{
    private struct Health
    {
        public int Value;
    }

    private struct Tag { }

    private struct Marker { }

    private static (World world, Entity[] entities) Populate(int count)
    {
        var world = new World();
        var entities = new Entity[count];
        for (var i = 0; i < count; i++)
        {
            entities[i] = world.CreateEntity();
            world.AddComponent(entities[i], new Health { Value = i });
        }
        return (world, entities);
    }

    [Fact]
    public void Remove_ShouldKeepRemainingComponentsReachable_WhenSwappingWithLast()
    {
        var (world, e) = Populate(5);

        world.RemoveComponent<Health>(e[1]);

        Assert.False(world.TryGetComponent<Health>(e[1], out _));
        Assert.Equal(4, world.GetStore<Health>().Count);
        foreach (var i in new[] { 0, 2, 3, 4 })
            Assert.Equal(i, world.GetComponent<Health>(e[i]).Value);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenComponentMissing()
    {
        var (world, e) = Populate(1);

        Assert.True(world.RemoveComponent<Health>(e[0]));
        Assert.False(world.RemoveComponent<Health>(e[0]));
    }

    [Fact]
    public void Add_ShouldOverwriteExistingComponent_WithoutChangingCount()
    {
        var (world, e) = Populate(3);

        world.AddComponent(e[1], new Health { Value = 99 });

        Assert.Equal(3, world.GetStore<Health>().Count);
        Assert.Equal(99, world.GetComponent<Health>(e[1]).Value);
    }

    [Fact]
    public void Get_ShouldThrowKeyNotFound_WhenComponentMissing()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.Throws<KeyNotFoundException>(() => world.GetComponent<Health>(entity));
    }

    [Fact]
    public void TryGet_ShouldWorkAcrossSparsePageBoundaries()
    {
        var (world, e) = Populate(3000);

        Assert.Equal(2999, world.GetComponent<Health>(e[2999]).Value);
        Assert.Equal(1500, world.GetComponent<Health>(e[1500]).Value);
        Assert.False(world.TryGetComponent<Tag>(e[2999], out _));
    }

    [Fact]
    public void GetComponentRef_ShouldMutateInPlace()
    {
        var (world, e) = Populate(3);

        world.GetComponentRef<Health>(e[2]).Value = 42;

        Assert.Equal(42, world.GetComponent<Health>(e[2]).Value);
    }

    [Fact]
    public void GetComponentRef_ShouldThrow_WhenComponentMissing()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.Throws<KeyNotFoundException>(() => world.GetComponentRef<Health>(entity));
    }

    [Fact]
    public void Enumerate_ShouldVisitEveryEntityOnce()
    {
        var (world, e) = Populate(10);
        world.RemoveComponent<Health>(e[3]);

        var seen = new List<int>();
        foreach (var (entity, health) in world.GetStore<Health>())
            seen.Add(entity.Id);

        Assert.Equal(9, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.DoesNotContain(e[3].Id, seen);
    }

    [Fact]
    public void Enumerate_ShouldAllowOverwritingComponentsDuringIteration()
    {
        var (world, _) = Populate(10);

        foreach (var (entity, health) in world.GetStore<Health>())
            world.AddComponent(entity, new Health { Value = health.Value + 1 });

        Assert.Equal(55, world.GetStore<Health>().All().Sum(p => p.Value.Value));
    }

    [Fact]
    public void Enumerate_ShouldThrow_WhenEntityRemovedDuringIteration()
    {
        var (world, e) = Populate(5);

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var _ in world.GetStore<Health>())
                world.RemoveComponent<Health>(e[0]);
        });
    }

    [Fact]
    public void Query_ShouldThrow_WhenDrivingStoreGainsEntityDuringIteration()
    {
        var (world, _) = Populate(5);
        foreach (var (entity, _) in world.GetStore<Health>().All().ToList())
            world.AddComponent(entity, new Tag());

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var (_, _, _) in world.Query<Health, Tag>())
                world.AddComponent(world.CreateEntity(), new Health());
        });
    }

    [Fact]
    public void Query_ShouldThrow_WhenNonDrivingStoreIsStructurallyModified()
    {
        var (world, _) = Populate(5);
        var extra = world.CreateEntity();
        foreach (var (entity, _) in world.GetStore<Health>().All().ToList())
            world.AddComponent(entity, new Tag());

        // Tag drives, but Health is still part of the query: whether a loop throws must not depend
        // on which store happens to be driving.
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var (_, _, _) in world.Query<Health, Tag>(forceIndex: 1))
                world.AddComponent(extra, new Health());
        });
    }

    [Fact]
    public void Query_ShouldThrow_WhenComponentRemovedRegardlessOfDrivingStore()
    {
        for (var forceIndex = 0; forceIndex <= 1; forceIndex++)
        {
            var (world, entities) = Populate(5);
            foreach (var entity in entities)
                world.AddComponent(entity, new Tag());

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var (entity, _, _) in world.Query<Health, Tag>(forceIndex))
                    world.RemoveComponent<Tag>(entity);
            });
        }
    }

    [Fact]
    public void Query_ShouldThrow_WhenLastVisitedEntityIsDestroyed()
    {
        var (world, entities) = Populate(3);
        foreach (var entity in entities)
            world.AddComponent(entity, new Tag());
        var last = entities[^1];

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var (entity, _, _) in world.Query<Health, Tag>())
            {
                if (entity == last)
                    world.DestroyEntity(entity);
            }
        });
    }

    [Fact]
    public void Query_ShouldAllowMutatingStoresOutsideTheQueryDuringIteration()
    {
        var (world, entities) = Populate(5);
        foreach (var entity in entities)
            world.AddComponent(entity, new Tag());

        foreach (var (entity, _, _) in world.Query<Health, Tag>())
        {
            world.AddComponent(entity, new Marker());
            world.AddComponent(world.CreateEntity(), new Marker());
        }

        Assert.Equal(10, world.GetStore<Marker>().Count);
    }

    [Fact]
    public void Query_ShouldAllowDestroyingEntities_WhenSnapshotted()
    {
        var (world, entities) = Populate(5);
        foreach (var entity in entities)
            world.AddComponent(entity, new Tag());

        foreach (var (entity, _, _) in world.Query<Health, Tag>().ToList())
            world.DestroyEntity(entity);

        Assert.Equal(0, world.GetStore<Health>().Count);
    }

    [Fact]
    public void Query_ShouldAllocateNothing()
    {
        var (world, e) = Populate(1000);
        foreach (var entity in e)
            world.AddComponent(entity, new Tag());
        var sum = 0;
        foreach (var (_, h, _) in world.Query<Health, Tag>()) // warm up / JIT
            sum += h.Value;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 10; pass++)
            foreach (var (_, h, _) in world.Query<Health, Tag>())
                sum += h.Value;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(sum > 0);
    }
}
