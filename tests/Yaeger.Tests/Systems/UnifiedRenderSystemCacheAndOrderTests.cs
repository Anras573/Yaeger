using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class UnifiedRenderSystemCacheAndOrderTests
{
    private sealed class FakeSurface : IRenderSurface
    {
        public readonly List<(
            Matrix4x4 Transform,
            string Path,
            Vector2 UvMin,
            Vector2 UvMax
        )> Quads = [];
        public Vector2 TextureSize = new(64f, 64f);
        public int SizeQueries;

        public void BeginFrame() => Quads.Clear();

        public void EndFrame() { }

        public void FlushQueuedQuads() { }

        public void SetCamera(Matrix4x4 viewProjection) { }

        public Vector2 GetTextureSize(string path)
        {
            SizeQueries++;
            return TextureSize;
        }

        public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) =>
            Quads.Add((transform, texturePath, Vector2.Zero, Vector2.One));

        public void SubmitQuad(
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) => Quads.Add((transform, texturePath, uvMin, uvMax));
    }

    private static Tilemap Map(params int[] tiles) =>
        new(new Tileset("t.png", columns: 2, rows: 2), tiles.Length, 1, tiles);

    private static (World World, Entity Entity) WorldWithMap(Tilemap map)
    {
        var world = new World();
        var e = world.CreateEntity();
        world.AddComponent(e, new Transform2D(Vector2.Zero));
        world.AddComponent(e, map);
        return (world, e);
    }

    [Fact]
    public void Render_TileEditedBetweenFrames_ShouldUseUpdatedUv()
    {
        var (world, e) = WorldWithMap(Map(0, 0));
        var surface = new FakeSurface();
        var system = new UnifiedRenderSystem(surface, null, world);
        system.Render();
        var before = surface.Quads[1].UvMin;

        world.GetComponentRef<Tilemap>(e).SetTile(1, 0, 3);
        system.Render();

        Assert.Equal(2, surface.Quads.Count);
        Assert.NotEqual(before, surface.Quads[1].UvMin);
        Assert.Equal(
            new Tileset("t.png", 2, 2).GetTileUv(3, surface.TextureSize).UvMin,
            surface.Quads[1].UvMin
        );
    }

    [Fact]
    public void Render_TextureSizeChanges_ShouldRebuildCachedUvs()
    {
        var (world, _) = WorldWithMap(Map(0));
        var surface = new FakeSurface { TextureSize = Vector2.Zero };
        var system = new UnifiedRenderSystem(surface, null, world);
        system.Render();
        var uninset = surface.Quads[0].UvMin;

        surface.TextureSize = new Vector2(64f, 64f); // texture finished loading
        system.Render();

        Assert.NotEqual(uninset, surface.Quads[0].UvMin);
    }

    [Fact]
    public void Render_StaticTilemap_ShouldSubmitIdenticalQuadsEveryFrame()
    {
        var (world, _) = WorldWithMap(Map(0, 1, 2, 3));
        var surface = new FakeSurface();
        var system = new UnifiedRenderSystem(surface, null, world);
        system.Render();
        var first = surface.Quads.ToList();

        system.Render();

        Assert.Equal(first, surface.Quads);
    }

    [Fact]
    public void Render_ShouldOrderByNegativeLayerBeforeNonNegativeThenEntityId()
    {
        var world = new World();
        var surface = new FakeSurface();
        (string Path, int Layer)[] specs = [("a", 5), ("b", -3), ("c", 0), ("d", -3)];
        foreach (var (path, layer) in specs)
        {
            var e = world.CreateEntity();
            world.AddComponent(e, new Transform2D(Vector2.Zero));
            world.AddComponent(e, new Sprite(path));
            world.AddComponent(e, new RenderLayer(layer));
        }

        new UnifiedRenderSystem(surface, null, world).Render();

        Assert.Equal(["b", "d", "c", "a"], surface.Quads.Select(q => q.Path));
    }
}
