using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class BoxBroadphaseTests
{
    private static Entity AddBox(World world, Vector2 position, Vector2 size)
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(position));
        world.AddComponent(entity, new BoxCollider2D(size));
        return entity;
    }

    private static List<Entity> QueryEntities(BoxBroadphase broadphase, Vector2 min, Vector2 max)
    {
        var ids = new List<int>();
        broadphase.Query(min, max, ids);
        return ids.Select(id => broadphase.Get(id).Entity).ToList();
    }

    [Fact]
    public void Query_ShouldReturnNothing_WhenRegionIsNaN()
    {
        // Arrange
        var world = new World();
        AddBox(world, new Vector2(0, 0), new Vector2(1, 1));
        AddBox(world, new Vector2(3, 0), new Vector2(1, 1));
        var broadphase = new BoxBroadphase(world, 1f, throwOnOversize: true);
        broadphase.Sync();

        // Act — every comparison against NaN is false, so an unguarded scan would let every
        // entry through.
        var hits = QueryEntities(broadphase, new Vector2(float.NaN, -1), new Vector2(float.NaN, 1));

        // Assert
        Assert.Empty(hits);
    }

    [Fact]
    public void Query_ShouldReturnOnlyCollidersOverlappingTheRegion_EachOnce()
    {
        // Arrange — one wide collider spanning many cells, plus a far one.
        var world = new World();
        var wide = AddBox(world, new Vector2(0, 0), new Vector2(10, 1));
        var far = AddBox(world, new Vector2(100, 100), new Vector2(1, 1));
        var broadphase = new BoxBroadphase(world, 1f, throwOnOversize: true);
        broadphase.Sync();

        // Act
        var hits = QueryEntities(broadphase, new Vector2(-6, -1), new Vector2(6, 1));

        // Assert
        Assert.Equal([wide], hits);
        Assert.DoesNotContain(far, hits);
    }

    [Fact]
    public void Sync_MovedCollider_ShouldBeFoundAtNewPositionOnly()
    {
        // Arrange
        var world = new World();
        var box = AddBox(world, Vector2.Zero, Vector2.One);
        var broadphase = new BoxBroadphase(world, 1f, throwOnOversize: true);
        broadphase.Sync();

        // Act
        world.AddComponent(box, new Transform2D(new Vector2(50, 50)));
        broadphase.Sync();

        // Assert
        Assert.Empty(QueryEntities(broadphase, new Vector2(-1, -1), new Vector2(1, 1)));
        Assert.Equal([box], QueryEntities(broadphase, new Vector2(49, 49), new Vector2(51, 51)));
    }

    [Fact]
    public void Sync_DestroyedCollider_ShouldBeRemoved()
    {
        // Arrange
        var world = new World();
        var box = AddBox(world, Vector2.Zero, Vector2.One);
        var broadphase = new BoxBroadphase(world, 1f, throwOnOversize: true);
        broadphase.Sync();

        // Act
        world.DestroyEntity(box);
        broadphase.Sync();

        // Assert
        Assert.Empty(QueryEntities(broadphase, new Vector2(-1, -1), new Vector2(1, 1)));
        Assert.Empty(broadphase.Ordered);
    }

    [Fact]
    public void Sync_UnchangedStaticGeometry_ShouldNotBumpStaticRevision()
    {
        // Arrange
        var world = new World();
        var wall = AddBox(world, Vector2.Zero, Vector2.One);
        world.AddComponent(wall, RigidBody2D.CreateStatic());
        var broadphase = new BoxBroadphase(world, 1f, throwOnOversize: true);
        broadphase.Sync();
        var revision = broadphase.StaticRevision;

        // Act
        broadphase.Sync();
        broadphase.Sync();

        // Assert
        Assert.Equal(revision, broadphase.StaticRevision);
    }

    [Fact]
    public void Query_ColliderTooLargeForHash_ShouldStillBeFoundWhenNotThrowing()
    {
        // Arrange — 1M x 1M units at cell size 1 is far beyond the per-insert cell limit.
        var world = new World();
        var huge = AddBox(world, Vector2.Zero, new Vector2(1_000_000, 1_000_000));
        var broadphase = new BoxBroadphase(world, 1f, throwOnOversize: false);

        // Act
        broadphase.Sync();

        // Assert
        Assert.Equal([huge], QueryEntities(broadphase, new Vector2(-1, -1), new Vector2(1, 1)));
    }

    [Fact]
    public void Sync_ColliderTooLargeForHash_ShouldThrowWhenConfiguredTo()
    {
        // Arrange
        var world = new World();
        AddBox(world, Vector2.Zero, new Vector2(1_000_000, 1_000_000));
        var broadphase = new BoxBroadphase(world, 1f, throwOnOversize: true);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => broadphase.Sync());
    }

    [Fact]
    public void Query_RegionTooLargeForHash_ShouldFallBackToScanningEverything()
    {
        // Arrange
        var world = new World();
        var a = AddBox(world, new Vector2(-500_000, 0), Vector2.One);
        var b = AddBox(world, new Vector2(500_000, 0), Vector2.One);
        var broadphase = new BoxBroadphase(world, 1f, throwOnOversize: true);
        broadphase.Sync();

        // Act
        var hits = QueryEntities(broadphase, new Vector2(-600_000, -1), new Vector2(600_000, 1));

        // Assert
        Assert.Equivalent(new[] { a, b }, hits);
    }
}
