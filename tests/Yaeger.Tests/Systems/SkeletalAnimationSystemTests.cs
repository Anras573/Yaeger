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
    public void Update_NegativeSpeedLooping_ShouldPlayInReverse()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: -1f) { Time = 0.2f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f); // 0.2 + (-1 * 0.5) = -0.3 -> wraps to 0.7

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0.7f, player.Time, 4);
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
    public void Update_NoClipWithNonFiniteTime_ShouldPersistSanitizedTime()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        // No clip is playing, but Time is non-finite: the sanitized value must be written back so it
        // doesn't linger in the store.
        world.AddComponent(entity, new AnimationPlayer(currentClip: null) { Time = float.NaN });

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0f, player.Time, 4);
    }

    [Fact]
    public void Update_NonFiniteSpeed_ShouldBeSanitizedAndPersisted()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: float.PositiveInfinity) { Time = 0.2f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0f, player.Speed, 4); // sanitized to 0 and persisted
        // Speed 0 means no advancement, so Time stays put.
        Assert.Equal(0.2f, player.Time, 4);
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

    // ── Crossfade ───────────────────────────────────────────────────────────

    // Single bone at the origin; "slide" moves +10 on X over 1s (from BuildRig), "rise" moves +10
    // on Y over 1s — independent axes make it easy to tell each clip's contribution to a blend
    // apart.
    private static (SkeletonRegistry Registry, SkeletonHandle Handle) BuildCrossfadeRig()
    {
        var registry = new SkeletonRegistry();
        var skeleton = new Skeleton(
            [new Bone("root", -1, Matrix4x4.Identity)],
            [Matrix4x4.Identity]
        );
        var slide = new AnimationClip(
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
        var rise = new AnimationClip(
            "rise",
            1f,
            [
                new BoneTrack(
                    0,
                    [new VectorKey(0f, Vector3.Zero), new VectorKey(1f, new Vector3(0f, 10f, 0f))],
                    [],
                    []
                ),
            ]
        );
        var handle = registry.Register(skeleton, [slide, rise]);
        return (registry, handle);
    }

    [Fact]
    public void CrossFadeTo_EntityWithoutAnimationPlayer_ThrowsInvalidOperationException()
    {
        var (registry, handle) = BuildCrossfadeRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle); // no AnimationPlayer

        var system = new SkeletalAnimationSystem(world, registry);

        Assert.Throws<InvalidOperationException>(() => system.CrossFadeTo(entity, "rise", 0.2f));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void CrossFadeTo_NonPositiveOrNonFiniteDuration_ActsAsHardSwitch(float duration)
    {
        var (registry, handle) = BuildCrossfadeRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0.4f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.CrossFadeTo(entity, "rise", duration);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal("rise", player.CurrentClip);
        Assert.Equal(0f, player.Time);
        Assert.Null(player.PreviousClip);
        Assert.Equal(0f, player.FadeDuration);
        Assert.Equal(0f, player.FadeElapsed);

        // No blending: the palette should reflect only the new clip's pose after one update.
        system.Update(0.3f);
        Assert.True(world.TryGetComponent<BonePalette>(entity, out var palette));
        Assert.Equal(0f, palette.Matrices[0].Translation.X, 4);
        Assert.Equal(3f, palette.Matrices[0].Translation.Y, 4);
    }

    [Fact]
    public void CrossFadeTo_PositiveDuration_CapturesCurrentClipAndTimeAsFadeSource()
    {
        var (registry, handle) = BuildCrossfadeRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0.4f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.CrossFadeTo(entity, "rise", 0.2f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal("rise", player.CurrentClip);
        Assert.Equal(0f, player.Time);
        Assert.Equal("slide", player.PreviousClip);
        Assert.Equal(0.4f, player.PreviousTime, 4);
        Assert.Equal(0.2f, player.FadeDuration, 4);
        Assert.Equal(0f, player.FadeElapsed);
    }

    [Fact]
    public void Update_DuringFade_BlendsBothClipsByElapsedFraction()
    {
        var (registry, handle) = BuildCrossfadeRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.CrossFadeTo(entity, "rise", duration: 1f);

        // Halfway through a 1s fade: both clips have also advanced to t=0.5 (from 0), so
        // slide.X = 5, rise.Y = 5; blended at alpha 0.5 -> (2.5, 2.5).
        system.Update(0.5f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0.5f, player.FadeElapsed, 4);
        Assert.False(player.FadeDuration <= player.FadeElapsed); // still fading

        Assert.True(world.TryGetComponent<BonePalette>(entity, out var palette));
        Assert.Equal(2.5f, palette.Matrices[0].Translation.X, 3);
        Assert.Equal(2.5f, palette.Matrices[0].Translation.Y, 3);
    }

    [Fact]
    public void Update_FadeElapses_DropsBackToSingleClipPathWithNoResidualBlend()
    {
        var (registry, handle) = BuildCrossfadeRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.CrossFadeTo(entity, "rise", duration: 0.2f);

        // deltaTime exceeds the fade duration, so the fade completes within this single update.
        system.Update(0.3f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Null(player.PreviousClip);
        Assert.Equal(0f, player.FadeDuration);
        Assert.Equal(0f, player.FadeElapsed);

        // Pure "rise" pose: no trace of "slide"'s X contribution.
        Assert.True(world.TryGetComponent<BonePalette>(entity, out var palette));
        Assert.Equal(0f, palette.Matrices[0].Translation.X, 4);
        Assert.Equal(3f, palette.Matrices[0].Translation.Y, 4);
    }

    [Fact]
    public void Update_DuringFade_LoopingFadeSourceKeepsAdvancingInsteadOfFreezing()
    {
        var (registry, handle) = BuildCrossfadeRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0.8f }
        );
        world.AddComponent(entity, handle);

        var system = new SkeletalAnimationSystem(world, registry);
        // A fade duration longer than the 1s source clip forces a wrap mid-fade if it's still
        // advancing.
        system.CrossFadeTo(entity, "rise", duration: 2f);

        system.Update(0.5f); // 0.8 + 0.5 = 1.3 -> wraps to 0.3, not frozen at 0.8

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0.3f, player.PreviousTime, 4);
    }

    [Fact]
    public void Update_DuringFade_RotationBlendsViaSlerpNotLinearInterpolation()
    {
        var registry = new SkeletonRegistry();
        var skeleton = new Skeleton(
            [new Bone("root", -1, Matrix4x4.Identity)],
            [Matrix4x4.Identity]
        );
        var identity = new AnimationClip(
            "identity",
            1f,
            [new BoneTrack(0, [], [new QuaternionKey(0f, Quaternion.Identity)], [])]
        );
        var rotated = new AnimationClip(
            "rotated",
            1f,
            [
                new BoneTrack(
                    0,
                    [],
                    [
                        new QuaternionKey(
                            0f,
                            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f)
                        ),
                    ],
                    []
                ),
            ]
        );
        var handle = registry.Register(skeleton, [identity, rotated]);

        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("identity", loop: true, speed: 1f));

        var system = new SkeletalAnimationSystem(world, registry);
        system.CrossFadeTo(entity, "rotated", duration: 1f);
        system.Update(0.5f); // alpha = 0.5

        var expectedRotation = Quaternion.Slerp(
            Quaternion.Identity,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f),
            0.5f
        );
        var expected = Vector3.Transform(Vector3.UnitZ, expectedRotation);

        Assert.True(world.TryGetComponent<BonePalette>(entity, out var palette));
        var actual = Vector3.Transform(Vector3.UnitZ, palette.Matrices[0]);
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }

    // ── IsFinished (issue #190) ─────────────────────────────────────────────

    [Fact]
    public void Update_NonLooping_ForwardReachesEnd_SetsIsFinished()
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
        Assert.True(player.IsFinished);

        // Stays finished (not a one-shot edge event) across further updates while clamped.
        system.Update(0.1f);
        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var stillFinished));
        Assert.True(stillFinished.IsFinished);
    }

    [Fact]
    public void Update_NonLooping_ReversePlaybackReachesStart_SetsIsFinished()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: false, speed: -1f) { Time = 0.1f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f); // clamps to 0.0 (the "end" when playing in reverse)

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0f, player.Time, 4);
        Assert.True(player.IsFinished);
    }

    [Fact]
    public void Update_NonLooping_BeforeReachingEnd_IsFinishedStaysFalse()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("slide", loop: false, speed: 1f));

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0.5f); // halfway, not yet at the end

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.False(player.IsFinished);
    }

    [Fact]
    public void Update_Looping_NeverSetsIsFinished()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0.9f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        for (var i = 0; i < 5; i++)
            system.Update(1f); // repeatedly wraps past the end

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.False(player.IsFinished);
    }

    [Fact]
    public void Update_IsFinished_ResetsWhenANewClipIsAssigned()
    {
        var (registry, handle) = BuildCrossfadeRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: false, speed: 1f) { Time = 1f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0f); // already at the end
        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var finished));
        Assert.True(finished.IsFinished);

        // A fresh AnimationPlayer for a different clip — the ordinary way to assign a new clip
        // directly (as opposed to CrossFadeTo).
        world.AddComponent(entity, new AnimationPlayer("rise", loop: false, speed: 1f));
        system.Update(0.1f); // not yet anywhere near "rise"'s own end

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal("rise", player.CurrentClip);
        Assert.False(player.IsFinished);
    }

    [Fact]
    public void Update_IsFinished_ResetsWhenThePlayerIsRestarted()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: false, speed: 1f) { Time = 1f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0f);
        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var finished));
        Assert.True(finished.IsFinished);

        // Same clip, rewound back to the start by hand — a restart.
        var restarted = finished;
        restarted.Time = 0f;
        world.AddComponent(entity, restarted);
        system.Update(0.1f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.False(player.IsFinished);
    }

    [Fact]
    public void Update_IsFinished_ClipClearedToNull_ResetsEvenWithoutOtherFieldChanges()
    {
        // Regression: IsFinished must be persisted even on a frame where nothing else about the
        // player changed enough to trigger the write-back (AdvanceTime no-ops without a clip).
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: false, speed: 1f) { Time = 1f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        system.Update(0f);
        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var finished));
        Assert.True(finished.IsFinished);

        var cleared = finished;
        cleared.CurrentClip = null;
        world.AddComponent(entity, cleared);
        system.Update(0.1f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.False(player.IsFinished);
    }

    // ── Animation events / markers (issue #190) ─────────────────────────────

    // Single bone rig whose clip carries named markers, for OnAnimationEvent tests.
    private static (SkeletonRegistry Registry, SkeletonHandle Handle) BuildRigWithEvents(
        float duration,
        params (float Time, string Key)[] markers
    )
    {
        var registry = new SkeletonRegistry();
        var skeleton = new Skeleton(
            [new Bone("root", -1, Matrix4x4.Identity)],
            [Matrix4x4.Identity]
        );
        var clip = new AnimationClip(
            "slide",
            duration,
            [
                new BoneTrack(
                    0,
                    [
                        new VectorKey(0f, Vector3.Zero),
                        new VectorKey(duration, new Vector3(10f, 0f, 0f)),
                    ],
                    [],
                    []
                ),
            ],
            [.. markers.Select(m => new AnimationEventMarker(m.Time, m.Key))]
        );
        var handle = registry.Register(skeleton, [clip]);
        return (registry, handle);
    }

    [Fact]
    public void Update_MarkerCrossedWithinFrame_FiresExactlyOnce()
    {
        var (registry, handle) = BuildRigWithEvents(2f, (0.5f, "a"), (1f, "b"), (1.5f, "c"));
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("slide", loop: false, speed: 1f));

        var system = new SkeletalAnimationSystem(world, registry);
        var fired = new List<AnimationEvent>();
        system.OnAnimationEvent += fired.Add;

        system.Update(0.6f); // crosses "a" (0.5) only

        var single = Assert.Single(fired);
        Assert.Equal(entity, single.Entity);
        Assert.Equal("slide", single.ClipName);
        Assert.Equal("a", single.Key);
    }

    [Fact]
    public void Update_LargeDeltaSpanningMultipleMarkers_AllFireInOrder()
    {
        var (registry, handle) = BuildRigWithEvents(2f, (0.5f, "a"), (1f, "b"), (1.5f, "c"));
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("slide", loop: false, speed: 1f));

        var system = new SkeletalAnimationSystem(world, registry);
        var fired = new List<string>();
        system.OnAnimationEvent += e => fired.Add(e.Key);

        system.Update(1.6f); // spans all three markers in one call

        Assert.Equal(["a", "b", "c"], fired);
    }

    [Fact]
    public void Update_ReversePlayback_MarkersFireInReverseOrder()
    {
        var (registry, handle) = BuildRigWithEvents(2f, (0.5f, "a"), (1f, "b"), (1.5f, "c"));
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: false, speed: -1f) { Time = 1.8f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        var fired = new List<string>();
        system.OnAnimationEvent += e => fired.Add(e.Key);

        system.Update(1.6f); // 1.8 -> 0.2, crossing c, b, a in that order

        Assert.Equal(["c", "b", "a"], fired);
    }

    [Fact]
    public void Update_LoopingClip_LargeDeltaSpanningMultipleLoops_FiresNearEndMarkerOncePerPass()
    {
        var (registry, handle) = BuildRigWithEvents(1f, (0.9f, "nearEnd"));
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: true, speed: 1f) { Time = 0.85f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        var fired = new List<string>();
        system.OnAnimationEvent += e => fired.Add(e.Key);

        // Travels 2.1s over a 1s looping clip starting just before the marker: crosses it on the
        // way to the first wrap, again on the second full pass, and again on the partial remainder.
        system.Update(2.1f);

        Assert.Equal(["nearEnd", "nearEnd", "nearEnd"], fired);
    }

    [Fact]
    public void Update_MarkerExactlyOnFrameBoundary_DoesNotDoubleFireAcrossConsecutiveFrames()
    {
        var (registry, handle) = BuildRigWithEvents(1f, (0.5f, "mid"));
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("slide", loop: false, speed: 1f));

        var system = new SkeletalAnimationSystem(world, registry);
        var fired = new List<string>();
        system.OnAnimationEvent += e => fired.Add(e.Key);

        system.Update(0.5f); // lands exactly on the marker: (0, 0.5] fires it once
        system.Update(0.1f); // continues from 0.5: (0.5, 0.6] must not refire it

        Assert.Equal(["mid"], fired);
    }

    [Fact]
    public void Update_ManualTimeJump_FiresNoMarkersForThatFrame()
    {
        var (registry, handle) = BuildRigWithEvents(2f, (0.5f, "a"), (1f, "b"), (1.5f, "c"));
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("slide", loop: false, speed: 1f));

        var system = new SkeletalAnimationSystem(world, registry);
        var fired = new List<string>();
        system.OnAnimationEvent += e => fired.Add(e.Key);

        system.Update(0.1f); // seeds the continuity baseline at Time = 0.1

        // A manual seek to just before "b" — not something Update itself produced.
        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var jumped));
        jumped.Time = 0.9f;
        world.AddComponent(entity, jumped);

        // This frame's own advance (0.9 -> 1.1) would ordinarily cross "b" (1.0) — but since the
        // starting point doesn't match what this system last computed, it's treated as a seek and
        // fires nothing.
        system.Update(0.2f);
        Assert.Empty(fired);

        // The following frame is continuous again (starts exactly where the last one left off), so
        // normal crossing detection resumes.
        system.Update(0.1f); // 1.1 -> 1.2: still doesn't reach "c" (1.5)
        Assert.Empty(fired);
    }

    [Fact]
    public void Update_ZeroDeltaTime_FiresNoMarkers()
    {
        var (registry, handle) = BuildRigWithEvents(2f, (0.5f, "a"));
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(
            entity,
            new AnimationPlayer("slide", loop: false, speed: 1f) { Time = 0.5f }
        );

        var system = new SkeletalAnimationSystem(world, registry);
        var fired = new List<string>();
        system.OnAnimationEvent += e => fired.Add(e.Key);

        system.Update(0f);

        Assert.Empty(fired);
    }

    [Fact]
    public void Update_DuringCrossFade_OnlyIncomingClipFiresMarkers()
    {
        var registry = new SkeletonRegistry();
        var skeleton = new Skeleton(
            [new Bone("root", -1, Matrix4x4.Identity)],
            [Matrix4x4.Identity]
        );
        var slide = new AnimationClip(
            "slideEvt",
            1f,
            [
                new BoneTrack(
                    0,
                    [new VectorKey(0f, Vector3.Zero), new VectorKey(1f, new Vector3(10f, 0f, 0f))],
                    [],
                    []
                ),
            ],
            [new AnimationEventMarker(0.15f, "slideMark")]
        );
        var rise = new AnimationClip(
            "riseEvt",
            1f,
            [
                new BoneTrack(
                    0,
                    [new VectorKey(0f, Vector3.Zero), new VectorKey(1f, new Vector3(0f, 10f, 0f))],
                    [],
                    []
                ),
            ],
            [new AnimationEventMarker(0.65f, "riseMark")]
        );
        var handle = registry.Register(skeleton, [slide, rise]);

        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        world.AddComponent(entity, new AnimationPlayer("slideEvt", loop: true, speed: 1f));

        var system = new SkeletalAnimationSystem(world, registry);
        var fired = new List<string>();
        system.OnAnimationEvent += e => fired.Add(e.Key);

        system.Update(0.1f); // seeds the "slideEvt" baseline at Time = 0.1

        system.CrossFadeTo(entity, "riseEvt", duration: 2f); // long fade: stays in progress below

        // First frame of the fade: the incoming clip's baseline doesn't match yet (it just reset
        // to "riseEvt"/0), so nothing fires here even though "riseEvt" is advancing. The outgoing
        // clip ("slideEvt") is also advancing (0.1 -> 0.4, crossing its own "slideMark" at 0.15)
        // but must never fire regardless of continuity — only the incoming clip ever can.
        system.Update(0.3f);
        Assert.Empty(fired);

        // Second frame: now continuous on "riseEvt", crossing (0.3, 0.7] -> "riseMark" (0.65).
        system.Update(0.4f);

        Assert.Equal(["riseMark"], fired);
    }
}
