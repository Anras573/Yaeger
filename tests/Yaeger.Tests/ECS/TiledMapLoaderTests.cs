using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class TiledMapLoaderTests
{
    // ── Tile layers ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleTileLayer_ShouldProduceOneEntityWithMatchingTilemap()
    {
        var loader = new TiledMapLoader();

        var scene = loader.Parse(
            """
            {
              "orientation": "orthogonal",
              "width": 2,
              "height": 2,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 2, "tilecount": 4 }
              ],
              "layers": [
                {
                  "type": "tilelayer",
                  "name": "ground",
                  "width": 2,
                  "height": 2,
                  "data": [0, 1, 2, 0]
                }
              ]
            }
            """
        );

        Assert.Equal(1, scene.EntityCount);

        var world = new World();
        var entities = world.Instantiate(scene);
        var entity = Assert.Single(entities);

        var tilemap = world.GetComponent<Tilemap>(entity);
        Assert.Equal(2, tilemap.Width);
        Assert.Equal(2, tilemap.Height);
        Assert.Equal(Tilemap.EmptyTile, tilemap.GetTile(0, 0));
        Assert.Equal(0, tilemap.GetTile(1, 0));
        Assert.Equal(1, tilemap.GetTile(0, 1));
        Assert.Equal(Tilemap.EmptyTile, tilemap.GetTile(1, 1));

        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(Vector2.Zero, transform.Position);

        var renderLayer = world.GetComponent<RenderLayer>(entity);
        Assert.Equal(0, renderLayer.Value);

        Assert.True(world.TryGetTag(entity, out var tag));
        Assert.Equal("ground", tag);
    }

    [Fact]
    public void Parse_MultipleTileLayers_ShouldAssignIncreasingRenderLayers()
    {
        var loader = new TiledMapLoader();

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                { "type": "tilelayer", "name": "back", "width": 1, "height": 1, "data": [1] },
                { "type": "tilelayer", "name": "front", "width": 1, "height": 1, "data": [1] }
              ]
            }
            """
        );

        var world = new World();
        var entities = world.Instantiate(scene);
        Assert.Equal(2, entities.Count);

        var back = world.GetEntity("back");
        var front = world.GetEntity("front");
        Assert.Equal(0, world.GetComponent<RenderLayer>(back).Value);
        Assert.Equal(1, world.GetComponent<RenderLayer>(front).Value);
    }

    [Fact]
    public void Parse_FlippedTileGid_ShouldStripFlagsAndMapToBaseTile()
    {
        var loader = new TiledMapLoader();

        // gid 2 (firstgid 1 + local id 1) with the horizontal-flip flag (bit 31) set.
        const uint horizontallyFlippedGid = 0x80000000u | 2u;

        var scene = loader.Parse(
            $$"""
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 2, "tilecount": 2 }
              ],
              "layers": [
                { "type": "tilelayer", "width": 1, "height": 1, "data": [{{horizontallyFlippedGid}}] }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(scene)[0];
        var tilemap = world.GetComponent<Tilemap>(entity);

        Assert.Equal(1, tilemap.GetTile(0, 0));
    }

    [Fact]
    public void Parse_TileLayerOffset_ShouldShiftWorldPosition()
    {
        var loader = new TiledMapLoader();

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                {
                  "type": "tilelayer",
                  "width": 1,
                  "height": 1,
                  "data": [1],
                  "offsetx": 16,
                  "offsety": 32
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(scene)[0];
        var transform = world.GetComponent<Transform2D>(entity);

        // offsetx of 16px (1 tile) moves +X; offsety of 32px (2 tiles) moves -Y (Tiled is Y-down).
        Assert.Equal(new Vector2(1f, -2f), transform.Position);
    }

    [Fact]
    public void Parse_CustomTileSizeAndOrigin_ShouldScaleAndTranslate()
    {
        var loader = new TiledMapLoader();

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                { "type": "tilelayer", "width": 1, "height": 1, "data": [1] }
              ]
            }
            """,
            tileSize: new Vector2(2f, 2f),
            origin: new Vector2(10f, 20f)
        );

        var world = new World();
        var entity = world.Instantiate(scene)[0];
        var transform = world.GetComponent<Transform2D>(entity);
        var tilemap = world.GetComponent<Tilemap>(entity);

        Assert.Equal(new Vector2(10f, 20f), transform.Position);
        Assert.Equal(new Vector2(2f, 2f), tilemap.TileSize);
    }

    // ── Solid tiles ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_TilesetSolidProperty_ShouldMarkTileSolid()
    {
        var loader = new TiledMapLoader();

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                {
                  "firstgid": 1,
                  "image": "tileset.png",
                  "columns": 2,
                  "tilecount": 2,
                  "tiles": [
                    {
                      "id": 0,
                      "properties": [ { "name": "solid", "type": "bool", "value": true } ]
                    },
                    {
                      "id": 1,
                      "properties": [ { "name": "solid", "type": "bool", "value": false } ]
                    }
                  ]
                }
              ],
              "layers": [
                { "type": "tilelayer", "width": 1, "height": 1, "data": [1] }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(scene)[0];
        var tilemap = world.GetComponent<Tilemap>(entity);

        Assert.True(tilemap.Tileset.IsSolid(0));
        Assert.False(tilemap.Tileset.IsSolid(1));
    }

    // ── Object layers / prefab spawning ─────────────────────────────────────

    [Fact]
    public void Parse_ObjectMatchingPrefab_ShouldSpawnPrefabAtConvertedPosition()
    {
        var loader = new TiledMapLoader();
        var prefab = new PrefabBuilder().With(new Sprite("Assets/player.png")).Build();
        var prefabsByName = new Dictionary<string, Prefab> { ["PlayerStart"] = prefab };

        var scene = loader.Parse(
            """
            {
              "width": 4,
              "height": 3,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                {
                  "type": "objectgroup",
                  "objects": [
                    {
                      "id": 1,
                      "name": "start",
                      "class": "PlayerStart",
                      "x": 8,
                      "y": 16,
                      "visible": true
                    }
                  ]
                }
              ]
            }
            """,
            prefabsByName
        );

        Assert.Equal(1, scene.EntityCount);

        var world = new World();
        var entity = world.Instantiate(scene)[0];

        // map height 3 tiles * 16px = 48px tall; x=8px -> 0.5 world units; y=16px from the top
        // -> (48 - 16) / 16 = 2.0 world units from the bottom.
        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(new Vector2(0.5f, 2.0f), transform.Position);

        var sprite = world.GetComponent<Sprite>(entity);
        Assert.Equal("Assets/player.png", sprite.TexturePath);

        Assert.True(world.TryGetTag(entity, out var tag));
        Assert.Equal("start", tag);
    }

    [Fact]
    public void Parse_HiddenObject_ShouldBeSkipped()
    {
        var loader = new TiledMapLoader();
        var prefab = new PrefabBuilder().With(new Sprite("Assets/player.png")).Build();
        var prefabsByName = new Dictionary<string, Prefab> { ["PlayerStart"] = prefab };

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                {
                  "type": "objectgroup",
                  "objects": [
                    { "id": 1, "class": "PlayerStart", "x": 0, "y": 0, "visible": false }
                  ]
                }
              ]
            }
            """,
            prefabsByName
        );

        Assert.Equal(0, scene.EntityCount);
    }

    [Fact]
    public void Parse_ObjectWithNoMatchingPrefab_ShouldBeSkippedWithoutError()
    {
        var loader = new TiledMapLoader();
        var prefabsByName = new Dictionary<string, Prefab>();

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                {
                  "type": "objectgroup",
                  "objects": [
                    { "id": 1, "class": "Unregistered", "x": 0, "y": 0, "visible": true }
                  ]
                }
              ]
            }
            """,
            prefabsByName
        );

        Assert.Equal(0, scene.EntityCount);
    }

    [Fact]
    public void Parse_ObjectLayerWithNoPrefabsByName_ShouldSkipAllObjects()
    {
        var loader = new TiledMapLoader();

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                {
                  "type": "objectgroup",
                  "objects": [
                    { "id": 1, "class": "PlayerStart", "x": 0, "y": 0, "visible": true }
                  ]
                }
              ]
            }
            """
        );

        Assert.Equal(0, scene.EntityCount);
    }

    [Fact]
    public void Parse_ObjectLegacyTypeField_ShouldFallBackWhenClassAbsent()
    {
        var loader = new TiledMapLoader();
        var prefab = new PrefabBuilder().With(new Sprite("Assets/enemy.png")).Build();
        var prefabsByName = new Dictionary<string, Prefab> { ["Enemy"] = prefab };

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                {
                  "type": "objectgroup",
                  "objects": [
                    { "id": 1, "type": "Enemy", "x": 0, "y": 0, "visible": true }
                  ]
                }
              ]
            }
            """,
            prefabsByName
        );

        Assert.Equal(1, scene.EntityCount);
    }

    // ── Validation / error handling ──────────────────────────────────────────

    [Fact]
    public void Parse_EmptyString_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() => loader.Parse(""));
    }

    [Fact]
    public void Parse_MalformedJson_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() => loader.Parse("{ not valid json"));
    }

    [Fact]
    public void Parse_NonObjectRoot_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() => loader.Parse("[1, 2, 3]"));
    }

    [Fact]
    public void Parse_MissingRequiredField_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse("""{ "width": 1, "height": 1, "tilewidth": 16, "tileheight": 16 }""")
        );
    }

    [Fact]
    public void Parse_NonOrthogonalOrientation_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        var ex = Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "orientation": "isometric",
                  "width": 1,
                  "height": 1,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
                  ],
                  "layers": []
                }
                """
            )
        );
        Assert.Contains("orthogonal", ex.Message);
    }

    [Fact]
    public void Parse_InfiniteMap_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "infinite": true,
                  "width": 1,
                  "height": 1,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
                  ],
                  "layers": []
                }
                """
            )
        );
    }

    [Fact]
    public void Parse_MultipleTilesets_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "width": 1,
                  "height": 1,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 1, "image": "a.png", "columns": 1, "tilecount": 1 },
                    { "firstgid": 2, "image": "b.png", "columns": 1, "tilecount": 1 }
                  ],
                  "layers": []
                }
                """
            )
        );
    }

    [Fact]
    public void Parse_ExternalTilesetSource_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        var ex = Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "width": 1,
                  "height": 1,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 1, "source": "tileset.tsj" }
                  ],
                  "layers": []
                }
                """
            )
        );
        Assert.Contains("source", ex.Message);
    }

    [Fact]
    public void Parse_UnsupportedEncoding_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "width": 1,
                  "height": 1,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
                  ],
                  "layers": [
                    { "type": "tilelayer", "width": 1, "height": 1, "encoding": "base64", "data": "AQAAAA==" }
                  ]
                }
                """
            )
        );
    }

    [Fact]
    public void Parse_CompressedLayer_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "width": 1,
                  "height": 1,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
                  ],
                  "layers": [
                    {
                      "type": "tilelayer",
                      "width": 1,
                      "height": 1,
                      "encoding": "base64",
                      "compression": "zlib",
                      "data": "eJwDAAAAAAE="
                    }
                  ]
                }
                """
            )
        );
    }

    [Fact]
    public void Parse_TileDataLengthMismatch_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "width": 2,
                  "height": 2,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
                  ],
                  "layers": [
                    { "type": "tilelayer", "width": 2, "height": 2, "data": [1, 1] }
                  ]
                }
                """
            )
        );
    }

    [Fact]
    public void Parse_GidLessThanFirstGid_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "width": 1,
                  "height": 1,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 5, "image": "tileset.png", "columns": 1, "tilecount": 1 }
                  ],
                  "layers": [
                    { "type": "tilelayer", "width": 1, "height": 1, "data": [2] }
                  ]
                }
                """
            )
        );
    }

    [Fact]
    public void Parse_TilesetTileCountNotDivisibleByColumns_ShouldThrow()
    {
        var loader = new TiledMapLoader();

        Assert.Throws<TiledMapLoadException>(() =>
            loader.Parse(
                """
                {
                  "width": 1,
                  "height": 1,
                  "tilewidth": 16,
                  "tileheight": 16,
                  "tilesets": [
                    { "firstgid": 1, "image": "tileset.png", "columns": 3, "tilecount": 4 }
                  ],
                  "layers": []
                }
                """
            )
        );
    }

    [Fact]
    public void Parse_UnknownLayerType_ShouldBeIgnored()
    {
        var loader = new TiledMapLoader();

        var scene = loader.Parse(
            """
            {
              "width": 1,
              "height": 1,
              "tilewidth": 16,
              "tileheight": 16,
              "tilesets": [
                { "firstgid": 1, "image": "tileset.png", "columns": 1, "tilecount": 1 }
              ],
              "layers": [
                { "type": "imagelayer", "name": "backdrop", "image": "sky.png" }
              ]
            }
            """
        );

        Assert.Equal(0, scene.EntityCount);
    }

    // ── Load(path) ────────────────────────────────────────────────────────────

    [Fact]
    public void Load_MissingFile_ShouldThrowFileNotFoundException()
    {
        var loader = new TiledMapLoader();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tmj");

        Assert.Throws<FileNotFoundException>(() => loader.Load(missingPath));
    }

    [Fact]
    public void Load_FixtureFile_ShouldLoadRenderAndSpawnPrefabEntities()
    {
        var loader = new TiledMapLoader();
        var prefab = new PrefabBuilder().With(new Sprite("Assets/player.png")).Build();
        var prefabsByName = new Dictionary<string, Prefab> { ["PlayerStart"] = prefab };

        // Checked-in fixture: 4x3 map, one solid-bottom-row tile layer, one object layer with
        // a visible PlayerStart, a hidden PlayerStart, and an unregistered decoration object.
        var scene = loader.Load("TestAssets/Tiled/level.tmj", prefabsByName);

        // One tile layer entity + one spawned PlayerStart (hidden object and the unregistered
        // decoration object are both skipped).
        Assert.Equal(2, scene.EntityCount);

        var world = new World();
        world.Instantiate(scene);

        var groundEntity = world.GetEntity("ground");
        var tilemap = world.GetComponent<Tilemap>(groundEntity);
        Assert.Equal(4, tilemap.Width);
        Assert.Equal(3, tilemap.Height);
        // Bottom row (row 2) is the solid tile (local id 0); rows above are empty.
        Assert.Equal(0, tilemap.GetTile(0, 2));
        Assert.True(tilemap.Tileset.IsSolid(tilemap.GetTile(0, 2)));
        Assert.Equal(Tilemap.EmptyTile, tilemap.GetTile(0, 0));

        var startEntity = world.GetEntity("start");
        Assert.Equal("Assets/player.png", world.GetComponent<Sprite>(startEntity).TexturePath);
    }
}
