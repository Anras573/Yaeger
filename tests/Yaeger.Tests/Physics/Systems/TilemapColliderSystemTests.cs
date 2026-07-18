using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class TilemapColliderSystemTests
{
    private const string TexturePath = "Assets/tiles.png";

    // Tile index 0 is solid ground; index 1 is a non-solid decoration.
    private static Tileset MakeTileset() => new(TexturePath, columns: 2, solidTileIndices: [0]);

    [Fact]
    public void Update_FlatFloor_ShouldMergeIntoOneCollider()
    {
        // Arrange — a 5-wide, 1-tall row of solid floor tiles.
        var world = new World();
        var tilemap = new Tilemap(MakeTileset(), width: 5, height: 1, tiles: [0, 0, 0, 0, 0]);

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, tilemap);

        var system = new TilemapColliderSystem(world);

        // Act
        system.Update(0f);

        // Assert — one collider entity was generated (not five), covering the whole span.
        var colliders = world.Query<BoxCollider2D, Transform2D>().ToList();
        var collider = Assert.Single(colliders);
        var (_, box, transform) = collider;
        Assert.Equal(new Vector2(5, 1), box.Size);
        Assert.Equal(new Vector2(2.5f, 0.5f), transform.Position);
    }

    [Fact]
    public void Update_GeneratedCollider_ShouldBeStatic()
    {
        var world = new World();
        var tilemap = new Tilemap(MakeTileset(), width: 1, height: 1, tiles: [0]);

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, tilemap);

        new TilemapColliderSystem(world).Update(0f);

        var (colliderEntity, _, _) = world.Query<BoxCollider2D, Transform2D>().Single();
        var body = world.GetComponent<RigidBody2D>(colliderEntity);
        Assert.Equal(BodyType.Static, body.Type);
    }

    [Fact]
    public void Update_NonSolidTiles_ShouldGenerateNoColliders()
    {
        var world = new World();
        // All tiles are index 1, which is not in the solid set.
        var tilemap = new Tilemap(MakeTileset(), width: 3, height: 1, tiles: [1, 1, 1]);

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, tilemap);

        new TilemapColliderSystem(world).Update(0f);

        Assert.Empty(world.Query<BoxCollider2D, Transform2D>().ToList());
    }

    [Fact]
    public void Update_EmptyTiles_ShouldGenerateNoColliders()
    {
        var world = new World();
        var tilemap = new Tilemap(MakeTileset(), width: 3, height: 1);

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, tilemap);

        new TilemapColliderSystem(world).Update(0f);

        Assert.Empty(world.Query<BoxCollider2D, Transform2D>().ToList());
    }

    [Fact]
    public void Update_DestroyingATile_ShouldRebuildCollidersWithinOneUpdate()
    {
        // Arrange — a 3-wide floor.
        var world = new World();
        var tilemap = new Tilemap(MakeTileset(), width: 3, height: 1, tiles: [0, 0, 0]);

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, tilemap);

        var system = new TilemapColliderSystem(world);
        system.Update(0f);
        Assert.Single(world.Query<BoxCollider2D, Transform2D>().ToList());

        // Act — break the middle tile (e.g. a destructible block).
        tilemap.SetTile(column: 1, row: 0, Tilemap.EmptyTile);
        system.Update(0f);

        // Assert — the single wide collider split into two, one per side of the gap.
        var colliders = world.Query<BoxCollider2D, Transform2D>().ToList();
        Assert.Equal(2, colliders.Count);
        var sizes = colliders.Select(c => c.Item2.Size).OrderBy(s => s.X).ToList();
        Assert.All(sizes, s => Assert.Equal(new Vector2(1, 1), s));
    }

    [Fact]
    public void Update_UnchangedTilemap_ShouldNotRegenerateColliderEntities()
    {
        var world = new World();
        var tilemap = new Tilemap(MakeTileset(), width: 3, height: 1, tiles: [0, 0, 0]);

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, tilemap);

        var system = new TilemapColliderSystem(world);
        system.Update(0f);
        var firstEntity = world.Query<BoxCollider2D, Transform2D>().Single().Item1;

        system.Update(0f);
        var secondEntity = world.Query<BoxCollider2D, Transform2D>().Single().Item1;

        Assert.Equal(firstEntity, secondEntity);
    }

    [Fact]
    public void Update_DestroyedTilemapEntity_ShouldRemoveItsGeneratedColliders()
    {
        var world = new World();
        var tilemap = new Tilemap(MakeTileset(), width: 2, height: 1, tiles: [0, 0]);

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, tilemap);

        var system = new TilemapColliderSystem(world);
        system.Update(0f);
        Assert.Single(world.Query<BoxCollider2D, Transform2D>().ToList());

        world.DestroyEntity(entity);
        system.Update(0f);

        Assert.Empty(world.Query<BoxCollider2D, Transform2D>().ToList());
    }

    [Fact]
    public void Update_TwoByTwoBlock_ShouldMergeIntoOneCollider()
    {
        var world = new World();
        var tilemap = new Tilemap(MakeTileset(), width: 2, height: 2, tiles: [0, 0, 0, 0]);

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(10, 20)));
        world.AddComponent(entity, tilemap);

        new TilemapColliderSystem(world).Update(0f);

        var (_, box, transform) = world.Query<BoxCollider2D, Transform2D>().Single();
        Assert.Equal(new Vector2(2, 2), box.Size);
        Assert.Equal(new Vector2(11, 21), transform.Position);
    }
}
