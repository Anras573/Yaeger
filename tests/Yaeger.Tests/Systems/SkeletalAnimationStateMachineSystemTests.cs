using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class SkeletalAnimationStateMachineSystemTests
{
    private static (SkeletonRegistry Registry, SkeletonHandle Handle) BuildRig()
    {
        var registry = new SkeletonRegistry();
        var skeleton = new Skeleton(
            [new Bone("root", -1, Matrix4x4.Identity)],
            [Matrix4x4.Identity]
        );
        var handle = registry.Register(skeleton, []);
        return (registry, handle);
    }

    private static (
        World World,
        Entity Entity,
        SkeletalAnimationStateMachineSystem System
    ) MakeMachine(
        bool restartOnReplay = false,
        SpeedThreshold[]? speedThresholds = null,
        float speedHysteresis = 0f,
        bool withPathFollow = false
    )
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);

        var states = new Dictionary<string, SkeletalState>
        {
            ["idle"] = new SkeletalState("idleClip"),
            ["walk"] = new SkeletalState("walkClip"),
            ["run"] = new SkeletalState("runClip"),
            ["jump"] = new SkeletalState("jumpClip", Loop: false),
        };
        world.AddComponent(
            entity,
            new SkeletalAnimationStateMachine(
                states,
                "idle",
                restartOnReplay: restartOnReplay,
                speedThresholds: speedThresholds,
                speedHysteresis: speedHysteresis
            )
        );

        if (withPathFollow)
            world.AddComponent(entity, new PathFollow3D([Vector3.Zero, Vector3.UnitX], speed: 1f));

        var animationSystem = new SkeletalAnimationSystem(world, registry);
        var system = new SkeletalAnimationStateMachineSystem(world, animationSystem);
        return (world, entity, system);
    }

    [Fact]
    public void Update_FreshMachine_BootstrapsAnimationPlayerFromInitialState()
    {
        var (world, entity, system) = MakeMachine();

        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal("idleClip", player.CurrentClip);
        Assert.True(player.Loop);
    }

    [Fact]
    public void Play_DifferentState_CrossfadesIntoNewClip()
    {
        var (world, entity, system) = MakeMachine();
        system.Update(0f);

        system.Play(entity, "walk");
        system.Update(0f);

        Assert.True(world.TryGetComponent<SkeletalAnimationStateMachine>(entity, out var machine));
        Assert.Equal("walk", machine.CurrentState);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal("walkClip", player.CurrentClip);
        Assert.Equal("idleClip", player.PreviousClip);
        Assert.True(player.FadeDuration > 0f);
    }

    [Fact]
    public void Play_StateWithDifferentLoopFlag_SyncsLoopOnPlayer()
    {
        var (world, entity, system) = MakeMachine();
        system.Update(0f);

        system.Play(entity, "jump");
        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal("jumpClip", player.CurrentClip);
        Assert.False(player.Loop);
    }

    [Fact]
    public void Play_TargetStateWithFadeDurationOverride_UsesItInsteadOfDefault()
    {
        var (registry, handle) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, handle);
        var states = new Dictionary<string, SkeletalState>
        {
            ["idle"] = new SkeletalState("idleClip"),
            ["attack"] = new SkeletalState("attackClip", Loop: false, FadeDuration: 0.05f),
        };
        world.AddComponent(
            entity,
            new SkeletalAnimationStateMachine(states, "idle", defaultFadeDuration: 0.3f)
        );
        var animationSystem = new SkeletalAnimationSystem(world, registry);
        var system = new SkeletalAnimationStateMachineSystem(world, animationSystem);
        system.Update(0f);

        system.Play(entity, "attack");
        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var player));
        Assert.Equal(0.05f, player.FadeDuration, 4);
    }

    [Fact]
    public void Play_SameLoopingStateWithoutRestartOnReplay_DoesNotCrossfadeAgain()
    {
        var (world, entity, system) = MakeMachine();
        system.Update(0f);
        // Advance the player's time so we can prove a no-op Play() doesn't reset it.
        var player = world.GetComponent<AnimationPlayer>(entity);
        player.Time = 0.5f;
        world.AddComponent(entity, player);

        system.Play(entity, "idle");
        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var after));
        Assert.Equal(0.5f, after.Time);
        Assert.Null(after.PreviousClip);
    }

    [Fact]
    public void Play_SameStateWithRestartOnReplay_CrossfadesAgain()
    {
        var (world, entity, system) = MakeMachine(restartOnReplay: true);
        system.Update(0f);
        var player = world.GetComponent<AnimationPlayer>(entity);
        player.Time = 0.5f;
        world.AddComponent(entity, player);

        system.Play(entity, "idle");
        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationPlayer>(entity, out var after));
        Assert.Equal(0f, after.Time);
        Assert.Equal("idleClip", after.PreviousClip);
    }

    [Fact]
    public void Play_UnknownState_ThrowsArgumentException()
    {
        var (_, entity, system) = MakeMachine();

        Assert.Throws<ArgumentException>(() => system.Play(entity, "nonexistent"));
    }

    [Fact]
    public void Play_EntityWithoutStateMachine_ThrowsInvalidOperationException()
    {
        var (registry, _) = BuildRig();
        var world = new World();
        var entity = world.CreateEntity();
        var animationSystem = new SkeletalAnimationSystem(world, registry);
        var system = new SkeletalAnimationStateMachineSystem(world, animationSystem);

        Assert.Throws<InvalidOperationException>(() => system.Play(entity, "idle"));
    }

    [Fact]
    public void Update_RequestedStateIsClearedAfterProcessing()
    {
        var (world, entity, system) = MakeMachine();
        system.Update(0f);

        system.Play(entity, "walk");
        system.Update(0f);

        Assert.True(world.TryGetComponent<SkeletalAnimationStateMachine>(entity, out var machine));
        Assert.Null(machine.RequestedState);
    }

    // ── Speed-driven selection ───────────────────────────────────────────────

    [Fact]
    public void Update_SpeedDrivenWithPathFollow_SelectsThresholdState()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f), new("run", 5f)];
        var (world, entity, system) = MakeMachine(
            speedThresholds: thresholds,
            withPathFollow: true
        );
        system.Update(0f); // bootstrap into idle

        var path = world.GetComponent<PathFollow3D>(entity);
        path.CurrentSpeed = 5f;
        world.AddComponent(entity, path);

        system.Update(0f);

        Assert.Equal("run", world.GetComponent<SkeletalAnimationStateMachine>(entity).CurrentState);
        Assert.Equal("runClip", world.GetComponent<AnimationPlayer>(entity).CurrentClip);
    }

    [Fact]
    public void Update_SpeedDrivenWithoutPathFollow_NeverAutoSwitches()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f)];
        var (world, entity, system) = MakeMachine(
            speedThresholds: thresholds,
            withPathFollow: false
        );

        system.Update(0f);
        system.Update(0f);

        Assert.Equal(
            "idle",
            world.GetComponent<SkeletalAnimationStateMachine>(entity).CurrentState
        );
    }

    [Fact]
    public void Play_TakesPriorityOverSpeedDrivenChoiceSameFrame()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f), new("run", 5f)];
        var (world, entity, system) = MakeMachine(
            speedThresholds: thresholds,
            withPathFollow: true
        );
        system.Update(0f);

        var path = world.GetComponent<PathFollow3D>(entity);
        path.CurrentSpeed = 5f; // would naturally select "run"
        world.AddComponent(entity, path);

        system.Play(entity, "jump");
        system.Update(0f);

        Assert.Equal(
            "jump",
            world.GetComponent<SkeletalAnimationStateMachine>(entity).CurrentState
        );
    }

    [Fact]
    public void ResolveSpeedDrivenState_UpwardCrossing_SwitchesImmediatelyAtThreshold()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f), new("run", 5f)];
        var machine = new SkeletalAnimationStateMachine(
            new Dictionary<string, SkeletalState>
            {
                ["idle"] = new SkeletalState("idleClip"),
                ["walk"] = new SkeletalState("walkClip"),
                ["run"] = new SkeletalState("runClip"),
            },
            "idle",
            speedThresholds: thresholds,
            speedHysteresis: 0.5f
        );

        var result = SkeletalAnimationStateMachineSystem.ResolveSpeedDrivenState(machine, 2f);

        Assert.Equal("walk", result);
    }

    [Fact]
    public void ResolveSpeedDrivenState_JustBelowOwnThresholdWithHysteresis_HoldsCurrentState()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f), new("run", 5f)];
        var machine = new SkeletalAnimationStateMachine(
            new Dictionary<string, SkeletalState>
            {
                ["idle"] = new SkeletalState("idleClip"),
                ["walk"] = new SkeletalState("walkClip"),
                ["run"] = new SkeletalState("runClip"),
            },
            "walk",
            speedThresholds: thresholds,
            speedHysteresis: 0.5f
        );

        // Below walk's own 2.0 threshold, but not past the 0.5 hysteresis margin (1.5).
        var result = SkeletalAnimationStateMachineSystem.ResolveSpeedDrivenState(machine, 1.8f);

        Assert.Equal("walk", result);
    }

    [Fact]
    public void ResolveSpeedDrivenState_PastHysteresisMargin_DropsToNaturalState()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f), new("run", 5f)];
        var machine = new SkeletalAnimationStateMachine(
            new Dictionary<string, SkeletalState>
            {
                ["idle"] = new SkeletalState("idleClip"),
                ["walk"] = new SkeletalState("walkClip"),
                ["run"] = new SkeletalState("runClip"),
            },
            "walk",
            speedThresholds: thresholds,
            speedHysteresis: 0.5f
        );

        // Below walk's threshold (2.0) minus hysteresis (0.5) = 1.5.
        var result = SkeletalAnimationStateMachineSystem.ResolveSpeedDrivenState(machine, 1.4f);

        Assert.Equal("idle", result);
    }

    [Fact]
    public void ResolveSpeedDrivenState_LargeSpeedDrop_SkipsDirectlyToNaturalState()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f), new("run", 5f)];
        var machine = new SkeletalAnimationStateMachine(
            new Dictionary<string, SkeletalState>
            {
                ["idle"] = new SkeletalState("idleClip"),
                ["walk"] = new SkeletalState("walkClip"),
                ["run"] = new SkeletalState("runClip"),
            },
            "run",
            speedThresholds: thresholds,
            speedHysteresis: 0.5f
        );

        var result = SkeletalAnimationStateMachineSystem.ResolveSpeedDrivenState(machine, 0f);

        Assert.Equal("idle", result);
    }

    [Fact]
    public void ResolveSpeedDrivenState_CurrentStateNotAThreshold_UsesNaturalStateDirectly()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f)];
        var machine = new SkeletalAnimationStateMachine(
            new Dictionary<string, SkeletalState>
            {
                ["idle"] = new SkeletalState("idleClip"),
                ["walk"] = new SkeletalState("walkClip"),
                ["jump"] = new SkeletalState("jumpClip", Loop: false),
            },
            "jump",
            speedThresholds: thresholds,
            speedHysteresis: 1f
        );

        var result = SkeletalAnimationStateMachineSystem.ResolveSpeedDrivenState(machine, 3f);

        Assert.Equal("walk", result);
    }
}
