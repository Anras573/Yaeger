using System.Numerics;
using Xunit;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class SkeletalAnimationSystemTests
{
    // Single bone at the origin in bind pose; a clip slides it +10 on X over one second.
    private static (SkeletonRegistry Registry, SkeletonHandle Handle) BuildRig()
    {
        var registry = new SkeletonRegistry();
        var skeleton = new Skeleton(
            [new Bone("root", -1, Matrix4x4.Identity)],
            [Matrix4x4.Identity]
        );
        var clip = new AnimationClip(
            "slide",
            1f,
            [
                new BoneTrack(
                    0,
                    [new VectorKey(0f, Vector3.Zero), new VectorKey(1f, new Vector3(10f, 0f, 0f))],
                    [],
                    []
                ),
            ]
        );
        var handle = registry.Register(skeleton, [clip]);
        return (registry, handle);
    }

    [Fact]
    public void Update_ShouldAdvanceTimeAndWritePalette()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("slide", loop: true, speed: 1f));

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0.5f, player.Time, 4);

        Assert.True(world.TryGetComponent<BonePalette>(entity, out var palette));
        Assert.Single(palette.Matrices);
        // The single bone has identity inverse bind, so the palette equals the sampled world
        // transform: a +5 X translation halfway through the slide.
        Assert.Equal(5f, palette.Matrices[0].Translation.X, 4);
    }

    [Fact]
    public void Update_Looping_ShouldWrapTime()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0.8f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f); // 0.8 + 0.5 = 1.3 -> wraps to 0.3

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0.3f, player.Time, 4);
    }

    [Fact]
    public void Update_NonLooping_ShouldClampAtDuration()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: false, speed: 1f) { Time = 0.9f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f); // clamps to 1.0

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(1f, player.Time, 4);
    }

    [Fact]
    public void Update_NoClip_ShouldWriteBindPosePalette()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer(currentClip: null));

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f);

        Assert.True(world.TryGetComponent<BonePalette>(entity, out var palette));
        // Bind pose with identity inverse bind -> identity palette.
        Assert.Equal(Matrix4x4.Identity, palette.Matrices[0]);
    }

    [Fact]
    public void Update_NegativeDelta_ShouldNotRewind()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0.4f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(-1f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0.4f, player.Time, 4);
    }

    [Fact]
    public void Update_WhenAnimationPlayerStoreIsSmaller_ShouldNotThrow()
    {
        // Regression: the system writes back AnimationPlayer while querying. If the query enumerated
        // the (smaller) AnimationPlayer store, the write would mutate it mid-iteration and throw.
        // Here there are more SkeletonHandle entities than AnimationPlayer entities, so an
        // unforced query would pick AnimationPlayer to iterate.
        var (registry, handle) = BuildRig();
        var world = new World();

        var animated = world.CreateEntity();
        world.AddComponent(animated, handle);
        world.AddComponent(animated, new AnimationPlayer("slide"));

        // Extra SkeletonHandle-only entities make the SkeletonHandle store the larger one.
        for (var i = 0; i < 3; i++)
            world.AddComponent(world.CreateEntity(), handle);

        var system = new SkeletalAnimationSystem(world, registry);

        var ex = Record.Exception(() => system.Update(0.1f));

        Assert.Null(ex);
        Assert.True(world.TryGetComponent<BonePalette>(animated, out _));
    }

    [Fact]
    public void Update_NonFiniteDelta_ShouldNotAdvanceOrCorruptTime()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0.4f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(float.NaN);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.True(float.IsFinite(player.Time));
        Assert.Equal(0.4f, player.Time, 4);
    }

    [Fact]
    public void Update_NonFiniteExistingTime_ShouldBeSanitized()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = float.NaN }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f); // NaN sanitized to 0, then advanced by 0.5

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0.5f, player.Time, 4);
    }

    [Fact]
    public void Update_ReusesPaletteArrayAcrossFrames()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("slide"));

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.1f);
        world.TryGetComponent<BonePalette>(entity, out var first);
        system.Update(0.1f);
        world.TryGetComponent<BonePalette>(entity, out var second);

        Assert.Same(first.Matrices, second.Matrices);
    }
}
