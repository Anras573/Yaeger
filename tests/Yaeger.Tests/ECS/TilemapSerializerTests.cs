using System.Numerics;
using System.Text.Json;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class TilemapSerializerTests
{
    private static PrefabLoader MakeLoader() =>
        new(new ComponentRegistry().RegisterEngineComponents());

    // ── Deserialization ──────────────────────────────────────────────────────

    [Fact]
    public void Deserialize_FullDefinition_ShouldPopulateAllFields()
    {
        var prefab = MakeLoader()
            .Parse(
                """
                {
                  "components": [
                    {
                      "type": "Tilemap",
                      "texturePath": "Assets/tiles.png",
                      "columns": 4,
                      "rows": 2,
                      "width": 3,
                      "height": 2,
                      "tileWidth": 2.0,
                      "tileHeight": 0.5,
                      "tiles": [0, 1, -1, 2, 3, 7],
                      "tint": [255, 0, 0, 128]
                    }
                  ]
                }
                """
            );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Tilemap>(entity, out var tilemap));
        Assert.Equal("Assets/tiles.png", tilemap.Tileset.TexturePath);
        Assert.Equal(4, tilemap.Tileset.Columns);
        Assert.Equal(2, tilemap.Tileset.Rows);
        Assert.Equal(3, tilemap.Width);
        Assert.Equal(2, tilemap.Height);
        Assert.Equal(new Vector2(2f, 0.5f), tilemap.TileSize);
        Assert.Equal(new[] { 0, 1, -1, 2, 3, 7 }, tilemap.Tiles);
        Assert.Equal(new Color(255, 0, 0, 128), tilemap.Tint);
    }

    [Fact]
    public void Deserialize_MinimalDefinition_ShouldApplyDefaults()
    {
        var prefab = MakeLoader()
            .Parse(
                """
                {
                  "components": [
                    {
                      "type": "Tilemap",
                      "texturePath": "Assets/tiles.png",
                      "columns": 4,
                      "width": 2,
                      "height": 3
                    }
                  ]
                }
                """
            );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Tilemap>(entity, out var tilemap));
        Assert.Equal(1, tilemap.Tileset.Rows);
        Assert.Equal(Vector2.One, tilemap.TileSize);
        Assert.Equal(Color.White, tilemap.Tint);
        Assert.All(tilemap.Tiles, tile => Assert.Equal(Tilemap.EmptyTile, tile));
    }

    [Fact]
    public void Deserialize_MissingTexturePath_ShouldThrow()
    {
        var ex = Assert.Throws<PrefabLoadException>(() =>
            MakeLoader()
                .Parse(
                    """{ "components": [ { "type": "Tilemap", "columns": 4, "width": 1, "height": 1 } ] }"""
                )
        );

        Assert.Contains("texturePath", ex.Message);
    }

    [Fact]
    public void Deserialize_NonPositiveWidth_ShouldThrow()
    {
        Assert.Throws<PrefabLoadException>(() =>
            MakeLoader()
                .Parse(
                    """
                    { "components": [ { "type": "Tilemap", "texturePath": "Assets/tiles.png", "columns": 4, "width": 0, "height": 1 } ] }
                    """
                )
        );
    }

    [Fact]
    public void Deserialize_NonPositiveTileWidth_ShouldThrow()
    {
        Assert.Throws<PrefabLoadException>(() =>
            MakeLoader()
                .Parse(
                    """
                    { "components": [ { "type": "Tilemap", "texturePath": "Assets/tiles.png", "columns": 4, "width": 1, "height": 1, "tileWidth": 0 } ] }
                    """
                )
        );
    }

    [Fact]
    public void Deserialize_WrongTileArrayLength_ShouldThrow()
    {
        var tooShort = Assert.Throws<PrefabLoadException>(() =>
            MakeLoader()
                .Parse(
                    """
                    { "components": [ { "type": "Tilemap", "texturePath": "Assets/tiles.png", "columns": 4, "width": 2, "height": 2, "tiles": [0, 1, 2] } ] }
                    """
                )
        );
        Assert.Contains("width * height", tooShort.Message);

        var tooLong = Assert.Throws<PrefabLoadException>(() =>
            MakeLoader()
                .Parse(
                    """
                    { "components": [ { "type": "Tilemap", "texturePath": "Assets/tiles.png", "columns": 4, "width": 2, "height": 2, "tiles": [0, 1, 2, 3, 0] } ] }
                    """
                )
        );
        Assert.Contains("width * height", tooLong.Message);
    }

    [Fact]
    public void Deserialize_TileIndexOutOfRange_ShouldThrow()
    {
        // columns 4, rows 1 → valid indices are -1..3, so 4 is out of range.
        var ex = Assert.Throws<PrefabLoadException>(() =>
            MakeLoader()
                .Parse(
                    """
                    { "components": [ { "type": "Tilemap", "texturePath": "Assets/tiles.png", "columns": 4, "width": 2, "height": 1, "tiles": [0, 4] } ] }
                    """
                )
        );

        Assert.Contains("tile index", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_NonIntegerTile_ShouldThrow()
    {
        Assert.Throws<PrefabLoadException>(() =>
            MakeLoader()
                .Parse(
                    """
                    { "components": [ { "type": "Tilemap", "texturePath": "Assets/tiles.png", "columns": 4, "width": 1, "height": 1, "tiles": [1.5] } ] }
                    """
                )
        );
    }

    // ── Serialization ────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_DefaultTileSizeAndTint_ShouldOmitOptionalFields()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Tilemap(new Tileset("Assets/tiles.png", 4), 2, 2));

        var json = new SceneSaver(registry).Serialize(world);

        using var doc = JsonDocument.Parse(json);
        var component = doc.RootElement.GetProperty("entities")[0].GetProperty("components")[0];
        Assert.False(component.TryGetProperty("tileWidth", out _));
        Assert.False(component.TryGetProperty("tileHeight", out _));
        Assert.False(component.TryGetProperty("tint", out _));
        Assert.Equal(4, component.GetProperty("tiles").GetArrayLength());
    }

    [Fact]
    public void SceneSaver_TilemapComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("level");
        var tilemap = new Tilemap(
            new Tileset("Assets/tiles.png", columns: 4, rows: 2),
            width: 3,
            height: 2,
            tiles: [0, 1, Tilemap.EmptyTile, 2, 3, 7],
            tileSize: new Vector2(2f, 0.5f),
            tint: new Color(10, 20, 30, 40)
        );
        world.AddComponent(entity, tilemap);

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        var reloadedEntity = reloaded.Entities.Single();
        Assert.True(reloaded.TryGetComponent<Tilemap>(reloadedEntity, out var reloadedMap));
        Assert.Equal(tilemap.Tileset.TexturePath, reloadedMap.Tileset.TexturePath);
        Assert.Equal(tilemap.Tileset.Columns, reloadedMap.Tileset.Columns);
        Assert.Equal(tilemap.Tileset.Rows, reloadedMap.Tileset.Rows);
        Assert.Equal(tilemap.Width, reloadedMap.Width);
        Assert.Equal(tilemap.Height, reloadedMap.Height);
        Assert.Equal(tilemap.TileSize, reloadedMap.TileSize);
        Assert.Equal(tilemap.Tint, reloadedMap.Tint);
        Assert.Equal(tilemap.Tiles, reloadedMap.Tiles);
    }
}
