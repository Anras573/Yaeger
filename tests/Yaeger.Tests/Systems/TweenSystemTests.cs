using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class TweenSystemTests
{
    [Fact]
    public void Update_OnceMode_Halfway_ShouldLerpPosition()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 2f
            )
        );

        var system = new TweenSystem(world);
        system.Update(1f);

        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(5f, transform.Position.X, 0.0001f);
        Assert.False(world.GetComponent<Tween>(entity).IsFinished);
    }

    [Fact]
    public void Update_OnceMode_PastEnd_ShouldSnapToToAndFinish()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 1f
            )
        );

        var system = new TweenSystem(world);
        system.Update(1.5f);

        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(10f, transform.Position.X, 0.0001f);
        Assert.True(world.GetComponent<Tween>(entity).IsFinished);
    }

    [Fact]
    public void Update_OnceMode_AfterFinished_FurtherUpdates_ShouldBeNoOp()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 1f
            )
        );

        var system = new TweenSystem(world);
        system.Update(1f);
        var elapsedAfterFinish = world.GetComponent<Tween>(entity).ElapsedTime;

        system.Update(5f);

        Assert.Equal(elapsedAfterFinish, world.GetComponent<Tween>(entity).ElapsedTime);
        Assert.Equal(10f, world.GetComponent<Transform2D>(entity).Position.X, 0.0001f);
    }

    [Fact]
    public void Update_LoopMode_ShouldWrapProgressAndNeverFinish()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 1f,
                loopMode: TweenLoopMode.Loop
            )
        );

        var system = new TweenSystem(world);
        system.Update(1.5f); // wraps: 1.5 % 1.0 == 0.5

        Assert.Equal(5f, world.GetComponent<Transform2D>(entity).Position.X, 0.0001f);
        Assert.False(world.GetComponent<Tween>(entity).IsFinished);
    }

    [Fact]
    public void Update_PingPongMode_PastMidpoint_ShouldReverseDirection()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 1f,
                loopMode: TweenLoopMode.PingPong
            )
        );

        var system = new TweenSystem(world);
        system.Update(1.25f); // cycle = 1.25 % 2 = 1.25 -> progress = 2 - 1.25 = 0.75, heading back

        Assert.Equal(7.5f, world.GetComponent<Transform2D>(entity).Position.X, 0.0001f);
        Assert.False(world.GetComponent<Tween>(entity).IsFinished);
    }

    [Fact]
    public void Update_WithDelay_BeforeDelayElapses_ShouldHoldAtFromValue()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 1f,
                delay: 0.5f
            )
        );

        var system = new TweenSystem(world);
        system.Update(0.2f);

        Assert.Equal(0f, world.GetComponent<Transform2D>(entity).Position.X, 0.0001f);
    }

    [Fact]
    public void Update_WithDelay_AfterDelayElapses_ShouldStartInterpolating()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 1f,
                delay: 0.5f
            )
        );

        var system = new TweenSystem(world);
        system.Update(1f); // elapsed 1.0, delay 0.5 -> active 0.5 -> progress 0.5

        Assert.Equal(5f, world.GetComponent<Transform2D>(entity).Position.X, 0.0001f);
    }

    [Fact]
    public void Update_Transform3DRotation_ShouldSlerpQuaternion()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var from = Quaternion.Identity;
        var to = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        world.AddComponent(entity, new Transform3D(Vector3.Zero, from, Vector3.One));
        world.AddComponent(
            entity,
            Tween.Create(entity, TweenChannel.Transform3DRotation, from, to, duration: 2f)
        );

        var system = new TweenSystem(world);
        system.Update(1f); // halfway

        var expected = Quaternion.Slerp(from, to, 0.5f);
        var actual = world.GetComponent<Transform3D>(entity).Rotation;
        Assert.Equal(expected.X, actual.X, 0.0001f);
        Assert.Equal(expected.Y, actual.Y, 0.0001f);
        Assert.Equal(expected.Z, actual.Z, 0.0001f);
        Assert.Equal(expected.W, actual.W, 0.0001f);
    }

    [Fact]
    public void Update_TargetDifferentFromCarrierEntity_ShouldWriteToTargetEntity()
    {
        var world = new World();
        var light = world.CreateEntity("light");
        world.AddComponent(light, PointLight.Default with { Intensity = 0f });
        var carrier = world.CreateEntity();
        world.AddComponent(
            carrier,
            Tween.Create(light, TweenChannel.PointLightIntensity, 0f, 1f, duration: 1f)
        );

        var system = new TweenSystem(world);
        system.Update(0.5f);

        Assert.Equal(0.5f, world.GetComponent<PointLight>(light).Intensity, 0.0001f);
    }

    [Fact]
    public void Update_TargetMissingComponent_ShouldNotThrow()
    {
        var world = new World();
        var target = world.CreateEntity(); // no Transform2D
        var carrier = world.CreateEntity();
        world.AddComponent(
            carrier,
            Tween.Create(
                target,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                Vector2.One,
                duration: 1f
            )
        );

        var system = new TweenSystem(world);
        var exception = Record.Exception(() => system.Update(0.5f));

        Assert.Null(exception);
    }

    [Fact]
    public void Update_NegativeDeltaTime_ShouldBeNoOp()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 1f
            )
        );

        var system = new TweenSystem(world);
        system.Update(-1f);

        Assert.Equal(0f, world.GetComponent<Tween>(entity).ElapsedTime);
        Assert.Equal(0f, world.GetComponent<Transform2D>(entity).Position.X, 0.0001f);
    }

    [Fact]
    public void Update_CubicInEasing_ShouldDifferFromLinearInterpolation()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(10f, 0f),
                duration: 1f,
                easing: EasingFunction.CubicIn
            )
        );

        var system = new TweenSystem(world);
        system.Update(0.5f);

        // CubicIn(0.5) == 0.125, not the 0.5 a linear interpolation would produce.
        Assert.Equal(1.25f, world.GetComponent<Transform2D>(entity).Position.X, 0.0001f);
    }

    [Fact]
    public void Update_MultipleTweensTargetingSameEntity_ShouldBothApply()
    {
        var world = new World();
        var door = world.CreateEntity("door");
        world.AddComponent(door, new Transform2D(Vector2.Zero));
        world.AddComponent(door, PointLight.Default with { Intensity = 0f });

        var positionCarrier = world.CreateEntity();
        world.AddComponent(
            positionCarrier,
            Tween.Create(
                door,
                TweenChannel.Transform2DPosition,
                Vector2.Zero,
                new Vector2(4f, 0f),
                duration: 1f
            )
        );

        var lightCarrier = world.CreateEntity();
        world.AddComponent(
            lightCarrier,
            Tween.Create(door, TweenChannel.PointLightIntensity, 0f, 2f, duration: 1f)
        );

        var system = new TweenSystem(world);
        system.Update(0.5f);

        Assert.Equal(2f, world.GetComponent<Transform2D>(door).Position.X, 0.0001f);
        Assert.Equal(1f, world.GetComponent<PointLight>(door).Intensity, 0.0001f);
    }

    [Fact]
    public void Update_ColorChannel_ShouldLerpColor()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, PointLight.Default with { Color = Color.Black });
        world.AddComponent(
            entity,
            Tween.Create(
                entity,
                TweenChannel.PointLightColor,
                Color.Black,
                Color.White,
                duration: 1f
            )
        );

        var system = new TweenSystem(world);
        system.Update(0.5f);

        var color = world.GetComponent<PointLight>(entity).Color.ToVector4();
        Assert.Equal(0.5f, color.X, 0.01f);
        Assert.Equal(0.5f, color.Y, 0.01f);
        Assert.Equal(0.5f, color.Z, 0.01f);
    }
}
