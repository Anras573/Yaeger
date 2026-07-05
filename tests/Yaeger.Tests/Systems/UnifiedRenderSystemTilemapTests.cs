using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class UnifiedRenderSystemTilemapTests
{
    private const string TexturePath = "Assets/tiles.png";

    private sealed class FakeRenderSurface : IRenderSurface
    {
        public readonly List<(
            Matrix4x4 Transform,
            string TexturePath,
            Vector2 UvMin,
            Vector2 UvMax,
            Vector4 Color
        )> Quads = [];

        public void BeginFrame() { }

        public void EndFrame() { }

        public void FlushQueuedQuads() { }

        public void SetCamera(Matrix4x4 viewProjection) { }

        public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) =>
            Quads.Add((transform, texturePath, Vector2.Zero, Vector2.One, color));

        public void SubmitQuad(
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) => Quads.Add((transform, texturePath, uvMin, uvMax, color));
    }

    private static Tileset MakeTileset() => new(TexturePath, columns: 2, rows: 2);

    // ── Rendering ────────────────────────────────────────────────────────────

    [Fact]
    public void Render_ShouldSubmitOneQuadPerNonEmptyTile()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            new Tilemap(MakeTileset(), width: 2, height: 2, tiles: [0, Tilemap.EmptyTile, 1, 2])
        );

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        Assert.Equal(3, surface.Quads.Count);
        Assert.All(surface.Quads, quad => Assert.Equal(TexturePath, quad.TexturePath));
    }

    [Fact]
    public void Render_ShouldPositionTilesBottomLeftOriginWithTopRowFirst()
    {
        // 2x2 map at world position (10, 20) with unit tiles. Row 0 is the top row, so
        // tile (column 0, row 0) is centred at (10.5, 21.5) and (column 1, row 1) at (11.5, 20.5).
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(10f, 20f)));
        world.AddComponent(
            entity,
            new Tilemap(
                MakeTileset(),
                width: 2,
                height: 2,
                tiles: [0, Tilemap.EmptyTile, Tilemap.EmptyTile, 1]
            )
        );

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        Assert.Equal(2, surface.Quads.Count);
        var topLeft = surface.Quads[0].Transform;
        Assert.Equal(10.5f, topLeft.M41, 0.0001f);
        Assert.Equal(21.5f, topLeft.M42, 0.0001f);
        var bottomRight = surface.Quads[1].Transform;
        Assert.Equal(11.5f, bottomRight.M41, 0.0001f);
        Assert.Equal(20.5f, bottomRight.M42, 0.0001f);
    }

    [Fact]
    public void Render_ShouldScaleQuadsToTileSize()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            new Tilemap(MakeTileset(), width: 1, height: 1, tiles: [0], new Vector2(2f, 0.5f))
        );

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        var transform = Assert.Single(surface.Quads).Transform;
        // Unit quad scaled to tile size: X basis length 2, Y basis length 0.5, centred in the cell.
        Assert.Equal(2f, transform.M11, 0.0001f);
        Assert.Equal(0.5f, transform.M22, 0.0001f);
        Assert.Equal(1f, transform.M41, 0.0001f);
        Assert.Equal(0.25f, transform.M42, 0.0001f);
    }

    [Fact]
    public void Render_ShouldUseTilesetUvForEachTileIndex()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Tilemap(MakeTileset(), width: 1, height: 1, tiles: [3]));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        var quad = Assert.Single(surface.Quads);
        var (expectedMin, expectedMax) = MakeTileset().GetTileUv(3);
        Assert.Equal(expectedMin, quad.UvMin);
        Assert.Equal(expectedMax, quad.UvMax);
    }

    [Fact]
    public void Render_ShouldApplyTintToAllTiles()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            new Tilemap(
                MakeTileset(),
                width: 2,
                height: 1,
                tiles: [0, 1],
                tint: new Color(255, 0, 0, 255)
            )
        );

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        Assert.All(
            surface.Quads,
            quad => Assert.Equal(new Color(255, 0, 0, 255).ToVector4(), quad.Color)
        );
    }

    [Fact]
    public void Render_ShouldOrderTilemapsBySpriteRenderLayer()
    {
        var world = new World();

        var foregroundSprite = world.CreateEntity();
        world.AddComponent(foregroundSprite, new Transform2D(Vector2.Zero));
        world.AddComponent(foregroundSprite, new Sprite("Assets/player.png"));
        world.AddComponent(foregroundSprite, new RenderLayer(1));

        var backgroundMap = world.CreateEntity();
        world.AddComponent(backgroundMap, new Transform2D(Vector2.Zero));
        world.AddComponent(
            backgroundMap,
            new Tilemap(MakeTileset(), width: 1, height: 1, tiles: [0])
        );
        world.AddComponent(backgroundMap, new RenderLayer(0));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        // Layer 0 tilemap first, layer 1 sprite on top.
        Assert.Equal(2, surface.Quads.Count);
        Assert.Equal(TexturePath, surface.Quads[0].TexturePath);
        Assert.Equal("Assets/player.png", surface.Quads[1].TexturePath);
    }

    // ── Culling ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetVisibleTileRange_CameraInsideMap_ShouldReturnSurroundingCells()
    {
        var map = new Tilemap(MakeTileset(), width: 200, height: 30);
        var transform = new Transform2D(Vector2.Zero);
        // Zoom 1, aspect 1 → visible world span is [99, 101] × [14, 16].
        var camera = new Camera2D(new Vector2(100f, 15f));

        var (columnMin, columnMax, rowMin, rowMax) = UnifiedRenderSystem.GetVisibleTileRange(
            map,
            transform,
            camera,
            aspectRatio: 1f
        );

        Assert.Equal(99, columnMin);
        Assert.Equal(100, columnMax);
        // Rows count from the top: world Y 14..16 is rows 14..15 of a 30-tall map.
        Assert.Equal(14, rowMin);
        Assert.Equal(15, rowMax);
    }

    [Fact]
    public void GetVisibleTileRange_ZoomAndAspect_ShouldShrinkAndWidenSpan()
    {
        var map = new Tilemap(MakeTileset(), width: 10, height: 10);
        var transform = new Transform2D(Vector2.Zero);
        // Zoom 2, aspect 2 → half extents (1, 0.5): visible span [4, 6] × [4.5, 5.5].
        var camera = new Camera2D(new Vector2(5f, 5f), Zoom: 2f);

        var (columnMin, columnMax, rowMin, rowMax) = UnifiedRenderSystem.GetVisibleTileRange(
            map,
            transform,
            camera,
            aspectRatio: 2f
        );

        Assert.Equal(4, columnMin);
        Assert.Equal(5, columnMax);
        Assert.Equal(4, rowMin);
        Assert.Equal(5, rowMax);
    }

    [Fact]
    public void GetVisibleTileRange_MapFullyOffScreen_ShouldReturnEmptyRange()
    {
        var map = new Tilemap(MakeTileset(), width: 10, height: 10);
        var transform = new Transform2D(Vector2.Zero);
        var camera = new Camera2D(new Vector2(-50f, 5f));

        var (columnMin, columnMax, _, _) = UnifiedRenderSystem.GetVisibleTileRange(
            map,
            transform,
            camera,
            aspectRatio: 1f
        );

        Assert.True(columnMin > columnMax);
    }

    [Fact]
    public void GetVisibleTileRange_RotatedMapTransform_ShouldFallBackToFullMap()
    {
        var map = new Tilemap(MakeTileset(), width: 10, height: 10);
        var transform = new Transform2D(Vector2.Zero, rotation: 0.5f);
        var camera = new Camera2D(new Vector2(100f, 100f));

        var range = UnifiedRenderSystem.GetVisibleTileRange(map, transform, camera, 1f);

        Assert.Equal((0, 9, 0, 9), range);
    }

    [Fact]
    public void GetVisibleTileRange_ScaledMapTransform_ShouldMapViewIntoLocalCells()
    {
        var map = new Tilemap(MakeTileset(), width: 10, height: 10);
        // Scale 2 → each cell covers 2 world units; view [3, 5] × [3, 5] is local [1.5, 2.5].
        var transform = new Transform2D(Vector2.Zero, scale: new Vector2(2f, 2f));
        var camera = new Camera2D(new Vector2(4f, 4f));

        var (columnMin, columnMax, rowMin, rowMax) = UnifiedRenderSystem.GetVisibleTileRange(
            map,
            transform,
            camera,
            aspectRatio: 1f
        );

        Assert.Equal(1, columnMin);
        Assert.Equal(2, columnMax);
        Assert.Equal(7, rowMin);
        Assert.Equal(8, rowMax);
    }

    [Fact]
    public void GetVisibleTileRange_RotatedCamera_ShouldExpandToConservativeBounds()
    {
        var map = new Tilemap(MakeTileset(), width: 100, height: 100);
        var transform = new Transform2D(Vector2.Zero);
        // 90° camera rotation with aspect 2 swaps the extents: (2, 1) → (1, 2).
        var camera = new Camera2D(new Vector2(50f, 50f), Rotation: MathF.PI / 2f);

        var (columnMin, columnMax, rowMin, rowMax) = UnifiedRenderSystem.GetVisibleTileRange(
            map,
            transform,
            camera,
            aspectRatio: 2f
        );

        // The ideal span is columns 49-50 and rows 48-51; the AABB is conservative and
        // float imprecision in cos/sin may widen it by a cell, but never shrink it.
        Assert.InRange(columnMin, 48, 49);
        Assert.InRange(columnMax, 50, 51);
        Assert.InRange(rowMin, 47, 48);
        Assert.InRange(rowMax, 51, 52);
    }
}
