using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class UnifiedRenderSystemSpriteFlipTests
{
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

    [Fact]
    public void Render_PlainSprite_Unflipped_SubmitsFullUnitUv()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Sprite("Assets/player.png"));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        var quad = Assert.Single(surface.Quads);
        Assert.Equal(Vector2.Zero, quad.UvMin);
        Assert.Equal(Vector2.One, quad.UvMax);
    }

    [Fact]
    public void Render_PlainSprite_FlippedX_SubmitsSwappedUBounds()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Sprite("Assets/player.png", flipX: true));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        var quad = Assert.Single(surface.Quads);
        Assert.Equal(new Vector2(1, 0), quad.UvMin);
        Assert.Equal(new Vector2(0, 1), quad.UvMax);
    }

    [Fact]
    public void Render_PlainSprite_FlippedY_SubmitsSwappedVBounds()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Sprite("Assets/player.png", flipY: true));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        var quad = Assert.Single(surface.Quads);
        Assert.Equal(new Vector2(0, 1), quad.UvMin);
        Assert.Equal(new Vector2(1, 0), quad.UvMax);
    }

    [Fact]
    public void Render_FlippedSprite_DoesNotSubmitExtraDrawCalls()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Sprite("Assets/player.png", flipX: true, flipY: true));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        Assert.Single(surface.Quads);
    }

    [Fact]
    public void Render_FlippedSprite_DoesNotAlterTransform()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var transform = new Transform2D(new Vector2(3f, 4f), scale: new Vector2(2f, 2f));
        world.AddComponent(entity, transform);
        world.AddComponent(entity, new Sprite("Assets/player.png", flipX: true));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        var quad = Assert.Single(surface.Quads);
        Assert.Equal(transform.TransformMatrix, quad.Transform);
    }

    [Fact]
    public void Render_SpriteSheetWithCoLocatedFlippedSprite_FlipsFrameSubRegion()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var sheet = new SpriteSheet("Assets/sheet.png", columns: 2, rows: 1);
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, sheet);
        world.AddComponent(entity, new AnimationState(CurrentFrameIndex: 1));
        world.AddComponent(entity, new Sprite("unused.png", flipX: true));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        var quad = Assert.Single(surface.Quads);
        var (expectedMin, expectedMax) = sheet.GetFrameUv(1);
        var (flippedMin, flippedMax) = Sprite.ApplyFlip(
            expectedMin,
            expectedMax,
            flipX: true,
            flipY: false
        );
        Assert.Equal(flippedMin, quad.UvMin);
        Assert.Equal(flippedMax, quad.UvMax);
    }

    [Fact]
    public void Render_SpriteSheetWithoutCoLocatedSprite_IsUnaffectedByFlip()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var sheet = new SpriteSheet("Assets/sheet.png", columns: 2, rows: 1);
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, sheet);
        world.AddComponent(entity, new AnimationState(CurrentFrameIndex: 1));

        var surface = new FakeRenderSurface();
        new UnifiedRenderSystem(surface, null, world).Render();

        var quad = Assert.Single(surface.Quads);
        var (expectedMin, expectedMax) = sheet.GetFrameUv(1);
        Assert.Equal(expectedMin, quad.UvMin);
        Assert.Equal(expectedMax, quad.UvMax);
    }
}
