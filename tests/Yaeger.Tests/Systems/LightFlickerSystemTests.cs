using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

/// <summary>
/// Tests for <see cref="LightFlickerSystem"/>: writing sampled intensity onto point/spot lights,
/// positional jitter tracking/restoration, and cleanup once a <see cref="LightFlicker"/> disappears.
/// </summary>
public class LightFlickerSystemTests
{
    [Fact]
    public void Update_WritesSampledIntensityOntoPointLight()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new LightFlicker
            {
                BaseIntensity = 1f,
                Amplitude = 0.3f,
                Frequency = 3f,
            }
        );
        world.AddComponent(entity, PointLight.Default);
        var system = new LightFlickerSystem(world);

        system.Update(0.1f);

        Assert.True(world.TryGetComponent<PointLight>(entity, out var light));
        Assert.InRange(light.Intensity, 0.7f, 1.3f);
    }

    [Fact]
    public void Update_WritesSampledIntensityOntoSpotLight()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new LightFlicker
            {
                BaseIntensity = 2f,
                Amplitude = 0.5f,
                Frequency = 3f,
            }
        );
        world.AddComponent(entity, SpotLight.Default);
        var system = new LightFlickerSystem(world);

        system.Update(0.1f);

        Assert.True(world.TryGetComponent<SpotLight>(entity, out var light));
        Assert.InRange(light.Intensity, 1.4f, 2.6f);
    }

    [Fact]
    public void Update_WritesBothPointAndSpotLightWhenBothPresent()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new LightFlicker { BaseIntensity = 1f, Frequency = 3f });
        world.AddComponent(entity, PointLight.Default);
        world.AddComponent(entity, SpotLight.Default);
        var system = new LightFlickerSystem(world);

        system.Update(0.1f);

        Assert.True(world.TryGetComponent<PointLight>(entity, out var point));
        Assert.True(world.TryGetComponent<SpotLight>(entity, out var spot));
        Assert.Equal(point.Intensity, spot.Intensity, precision: 5);
    }

    [Fact]
    public void Update_IntensityNeverGoesNegative()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new LightFlicker
            {
                BaseIntensity = 0.01f,
                Amplitude = 10f, // wildly larger than base, would go deeply negative unclamped
                Frequency = 5f,
            }
        );
        world.AddComponent(entity, PointLight.Default);
        var system = new LightFlickerSystem(world);

        for (var i = 0; i < 50; i++)
        {
            system.Update(0.05f);
            Assert.True(world.TryGetComponent<PointLight>(entity, out var light));
            Assert.True(light.Intensity >= 0f);
        }
    }

    [Fact]
    public void Update_AdvancesElapsedByDeltaTime()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new LightFlicker { BaseIntensity = 1f, Frequency = 3f });
        var system = new LightFlickerSystem(world);

        system.Update(0.4f);
        system.Update(0.4f);

        Assert.True(world.TryGetComponent<LightFlicker>(entity, out var flicker));
        Assert.Equal(0.8f, flicker.Elapsed, precision: 4);
    }

    [Fact]
    public void Update_DifferentSeeds_ProduceDifferentIntensities()
    {
        var world = new World();
        var a = world.CreateEntity();
        world.AddComponent(
            a,
            new LightFlicker
            {
                BaseIntensity = 1f,
                Amplitude = 0.5f,
                Frequency = 3f,
                Seed = 1f,
            }
        );
        world.AddComponent(a, PointLight.Default);

        var b = world.CreateEntity();
        world.AddComponent(
            b,
            new LightFlicker
            {
                BaseIntensity = 1f,
                Amplitude = 0.5f,
                Frequency = 3f,
                Seed = 99f,
            }
        );
        world.AddComponent(b, PointLight.Default);

        var system = new LightFlickerSystem(world);
        system.Update(0.3f);

        Assert.True(world.TryGetComponent<PointLight>(a, out var lightA));
        Assert.True(world.TryGetComponent<PointLight>(b, out var lightB));
        Assert.NotEqual(lightA.Intensity, lightB.Intensity);
    }

    [Fact]
    public void Update_WithoutPositionJitter_LeavesTransformUntouched()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new LightFlicker { BaseIntensity = 1f, Frequency = 3f });
        world.AddComponent(entity, PointLight.Default);
        world.AddComponent(
            entity,
            new Transform3D(new Vector3(5f, 1f, -2f), Quaternion.Identity, Vector3.One)
        );
        var system = new LightFlickerSystem(world);

        system.Update(0.1f);

        Assert.True(world.TryGetComponent<Transform3D>(entity, out var transform));
        Assert.Equal(new Vector3(5f, 1f, -2f), transform.Position);
    }

    [Fact]
    public void Update_WithPositionJitter_OffsetsTransformWithinRadius()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new LightFlicker
            {
                BaseIntensity = 1f,
                Frequency = 3f,
                PositionJitter = 0.2f,
            }
        );
        world.AddComponent(entity, PointLight.Default);
        world.AddComponent(
            entity,
            new Transform3D(new Vector3(5f, 1f, -2f), Quaternion.Identity, Vector3.One)
        );
        var system = new LightFlickerSystem(world);

        system.Update(0.1f);

        Assert.True(world.TryGetComponent<Transform3D>(entity, out var transform));
        var offset = transform.Position - new Vector3(5f, 1f, -2f);
        Assert.True(offset.Length() <= 0.2f * MathF.Sqrt(3) + 0.0001f);
        Assert.NotEqual(Vector3.Zero, offset);
    }

    [Fact]
    public void Update_WithPositionJitter_UndoesPreviousOffsetBeforeApplyingANewOne()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new LightFlicker
            {
                BaseIntensity = 1f,
                Frequency = 3f,
                PositionJitter = 0.2f,
            }
        );
        world.AddComponent(entity, PointLight.Default);
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        var system = new LightFlickerSystem(world);

        // If the system didn't undo the previous frame's offset, many steps would let jitter
        // accumulate arbitrarily far from the base position instead of staying bounded near it.
        for (var i = 0; i < 100; i++)
            system.Update(0.05f);

        Assert.True(world.TryGetComponent<Transform3D>(entity, out var transform));
        Assert.True(transform.Position.Length() <= 0.2f * MathF.Sqrt(3) + 0.0001f);
    }

    [Fact]
    public void Update_AfterComponentRemoved_RestoresBaseIntensity()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new LightFlicker
            {
                BaseIntensity = 1f,
                Amplitude = 0.9f,
                Frequency = 3f,
            }
        );
        world.AddComponent(entity, PointLight.Default);
        var system = new LightFlickerSystem(world);

        system.Update(0.1f);
        world.RemoveComponent<LightFlicker>(entity);
        system.Update(0.1f);

        Assert.True(world.TryGetComponent<PointLight>(entity, out var light));
        Assert.Equal(1f, light.Intensity, precision: 4);
    }

    [Fact]
    public void Update_AfterComponentRemoved_RestoresPositionOffset()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new LightFlicker
            {
                BaseIntensity = 1f,
                Frequency = 3f,
                PositionJitter = 0.5f,
            }
        );
        world.AddComponent(entity, PointLight.Default);
        world.AddComponent(
            entity,
            new Transform3D(new Vector3(10f, 0f, 0f), Quaternion.Identity, Vector3.One)
        );
        var system = new LightFlickerSystem(world);

        system.Update(0.1f);
        world.RemoveComponent<LightFlicker>(entity);
        system.Update(0.1f);

        Assert.True(world.TryGetComponent<Transform3D>(entity, out var transform));
        Assert.Equal(new Vector3(10f, 0f, 0f), transform.Position);
    }

    [Fact]
    public void Update_AfterEntityDestroyed_DoesNotThrow()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new LightFlicker { BaseIntensity = 1f, Frequency = 3f });
        world.AddComponent(entity, PointLight.Default);
        var system = new LightFlickerSystem(world);

        system.Update(0.1f);
        world.DestroyEntity(entity);

        var exception = Record.Exception(() => system.Update(0.1f));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Update_InvalidDeltaTime_IsIgnored(float deltaTime)
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new LightFlicker { BaseIntensity = 1f, Frequency = 3f });
        world.AddComponent(entity, PointLight.Default);
        var system = new LightFlickerSystem(world);

        system.Update(deltaTime);

        Assert.True(world.TryGetComponent<LightFlicker>(entity, out var flicker));
        Assert.Equal(0f, flicker.Elapsed);
        Assert.True(world.TryGetComponent<PointLight>(entity, out var light));
        Assert.Equal(PointLight.Default.Intensity, light.Intensity);
    }

    [Fact]
    public void Update_WithoutAnyLightComponent_StillAdvancesElapsedWithoutThrowing()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new LightFlicker { BaseIntensity = 1f, Frequency = 3f });
        var system = new LightFlickerSystem(world);

        var exception = Record.Exception(() => system.Update(0.1f));

        Assert.Null(exception);
        Assert.True(world.TryGetComponent<LightFlicker>(entity, out var flicker));
        Assert.Equal(0.1f, flicker.Elapsed, precision: 4);
    }
}
