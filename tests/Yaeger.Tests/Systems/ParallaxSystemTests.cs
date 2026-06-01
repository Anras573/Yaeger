using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class ParallaxSystemTests
{
    [Fact]
    public void Update_WithScrollFactorZero_ShouldFixLayerToScreen()
    {
        // A factor of 0 means the layer tracks the camera completely → appears
        // fixed on screen regardless of where the camera is.
        var world = new World();
        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(new Vector2(100f, 60f)));

        var layerEntity = world.CreateEntity();
        world.AddComponent(layerEntity, new Transform2D(Vector2.Zero));
        world.AddComponent(layerEntity, new ParallaxLayer(scrollFactorX: 0f, scrollFactorY: 0f));

        new ParallaxSystem(world).Update(0f);

        var transform = world.GetComponent<Transform2D>(layerEntity);
        // worldPos = basePos(0,0) + cameraPos(100,60) * (1 - 0) = (100, 60)
        // screenPos = worldPos - cameraPos = (0, 0)  → fixed on screen
        Assert.Equal(100f, transform.Position.X, 0.001f);
        Assert.Equal(60f, transform.Position.Y, 0.001f);
    }

    [Fact]
    public void Update_WithScrollFactorOne_ShouldLeaveLayerWorldFixed()
    {
        // A factor of 1 means no tracking adjustment → layer stays at BasePosition,
        // which is the same as any regular game entity.
        var world = new World();
        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(new Vector2(100f, 0f)));

        var layerEntity = world.CreateEntity();
        world.AddComponent(layerEntity, new Transform2D(Vector2.Zero));
        world.AddComponent(layerEntity, new ParallaxLayer(scrollFactorX: 1f, scrollFactorY: 1f));

        new ParallaxSystem(world).Update(0f);

        var transform = world.GetComponent<Transform2D>(layerEntity);
        // worldPos = basePos(0,0) + cameraPos(100,0) * (1 - 1) = (0, 0)
        Assert.Equal(0f, transform.Position.X, 0.001f);
        Assert.Equal(0f, transform.Position.Y, 0.001f);
    }

    [Fact]
    public void Update_WithHalfScrollFactor_ShouldScrollAtHalfCameraSpeed()
    {
        var world = new World();
        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(new Vector2(100f, 0f)));

        var layerEntity = world.CreateEntity();
        world.AddComponent(layerEntity, new Transform2D(Vector2.Zero));
        world.AddComponent(layerEntity, new ParallaxLayer(scrollFactorX: 0.5f, scrollFactorY: 0f));

        new ParallaxSystem(world).Update(0f);

        var transform = world.GetComponent<Transform2D>(layerEntity);
        // worldPos.X = 0 + 100 * (1 - 0.5) = 50
        // screenPos.X = 50 - 100 = -50  → half camera speed
        Assert.Equal(50f, transform.Position.X, 0.001f);
        // scrollFactorY=0 → worldPos.Y = 0 + 0 * 1 = 0
        Assert.Equal(0f, transform.Position.Y, 0.001f);
    }

    [Fact]
    public void Update_WithNoCameraEntity_ShouldPositionLayerAtBasePosition()
    {
        var world = new World();
        var layerEntity = world.CreateEntity();
        world.AddComponent(layerEntity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            layerEntity,
            new ParallaxLayer(scrollFactorX: 0.3f) { BasePosition = new Vector2(10f, 5f) }
        );

        new ParallaxSystem(world).Update(0f);

        var transform = world.GetComponent<Transform2D>(layerEntity);
        // cameraPos defaults to (0,0) → worldPos = basePos + (0,0) * anything = basePos
        Assert.Equal(10f, transform.Position.X, 0.001f);
        Assert.Equal(5f, transform.Position.Y, 0.001f);
    }

    [Fact]
    public void Update_ShouldRespectNonZeroBasePosition()
    {
        var world = new World();
        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(new Vector2(50f, 0f)));

        var layerEntity = world.CreateEntity();
        world.AddComponent(layerEntity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            layerEntity,
            new ParallaxLayer(scrollFactorX: 0f) { BasePosition = new Vector2(200f, 0f) }
        );

        new ParallaxSystem(world).Update(0f);

        var transform = world.GetComponent<Transform2D>(layerEntity);
        // worldPos.X = 200 + 50 * (1 - 0) = 250
        Assert.Equal(250f, transform.Position.X, 0.001f);
    }

    [Fact]
    public void Update_ShouldNotAffectEntitiesWithoutParallaxLayer()
    {
        var world = new World();
        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(new Vector2(100f, 0f)));

        var regularEntity = world.CreateEntity();
        world.AddComponent(regularEntity, new Transform2D(new Vector2(42f, 7f)));

        new ParallaxSystem(world).Update(0f);

        var transform = world.GetComponent<Transform2D>(regularEntity);
        Assert.Equal(42f, transform.Position.X, 0.001f);
        Assert.Equal(7f, transform.Position.Y, 0.001f);
    }

    [Fact]
    public void Update_ShouldPreserveScaleAndRotation()
    {
        var world = new World();
        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(new Vector2(50f, 0f)));

        var layerEntity = world.CreateEntity();
        world.AddComponent(
            layerEntity,
            new Transform2D(Vector2.Zero, rotation: 1.5f, scale: new Vector2(3f, 2f))
        );
        world.AddComponent(layerEntity, new ParallaxLayer(scrollFactorX: 0.5f));

        new ParallaxSystem(world).Update(0f);

        var transform = world.GetComponent<Transform2D>(layerEntity);
        Assert.Equal(1.5f, transform.Rotation, 0.001f);
        Assert.Equal(3f, transform.Scale.X, 0.001f);
        Assert.Equal(2f, transform.Scale.Y, 0.001f);
    }

    [Fact]
    public void Update_WithMultipleLayers_ShouldScrollEachIndependently()
    {
        var world = new World();
        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(new Vector2(100f, 0f)));

        var farLayer = world.CreateEntity();
        world.AddComponent(farLayer, new Transform2D(Vector2.Zero));
        world.AddComponent(farLayer, new ParallaxLayer(scrollFactorX: 0.1f));

        var nearLayer = world.CreateEntity();
        world.AddComponent(nearLayer, new Transform2D(Vector2.Zero));
        world.AddComponent(nearLayer, new ParallaxLayer(scrollFactorX: 0.8f));

        new ParallaxSystem(world).Update(0f);

        var farTransform = world.GetComponent<Transform2D>(farLayer);
        var nearTransform = world.GetComponent<Transform2D>(nearLayer);

        // Far layer: worldPos.X = 100 * (1 - 0.1) = 90
        Assert.Equal(90f, farTransform.Position.X, 0.001f);
        // Near layer: worldPos.X = 100 * (1 - 0.8) = 20
        Assert.Equal(20f, nearTransform.Position.X, 0.001f);
    }
}
