using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class AnimationStateMachineSystemTests
{
    private static Animation MakeClip(string texturePath, bool loop = true) =>
        new([new AnimationFrame(texturePath, 0.1f), new AnimationFrame(texturePath, 0.1f)], loop);

    private static (World world, Entity entity, AnimationStateMachineSystem system) MakeMachine(
        bool restartOnReplay = false
    )
    {
        var world = new World();
        var entity = world.CreateEntity();
        var states = new Dictionary<string, Animation>
        {
            ["idle"] = MakeClip("idle.png"),
            ["jump"] = MakeClip("jump.png", loop: false),
        };
        world.AddComponent(entity, new AnimationStateMachine(states, "idle", restartOnReplay));
        var system = new AnimationStateMachineSystem(world);
        return (world, entity, system);
    }

    [Fact]
    public void Update_FreshMachine_BootstrapsAnimationFromInitialState()
    {
        var (world, entity, system) = MakeMachine();

        system.Update(0f);

        Assert.True(world.TryGetComponent<Animation>(entity, out var animation));
        Assert.Equal("idle.png", animation.Frames[0].TexturePath);
        Assert.True(world.TryGetComponent<AnimationState>(entity, out var state));
        Assert.Equal(0, state.CurrentFrameIndex);
    }

    [Fact]
    public void Play_DifferentState_SwitchesClipAndResetsFrameToZero()
    {
        var (world, entity, system) = MakeMachine();
        system.Update(0f);

        // Advance the idle clip's frame so we can prove Play() resets it.
        world.AddComponent(entity, new AnimationState(CurrentFrameIndex: 1, ElapsedTime: 0.05f));

        system.Play(entity, "jump");
        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationStateMachine>(entity, out var machine));
        Assert.Equal("jump", machine.CurrentState);
        Assert.True(world.TryGetComponent<AnimationState>(entity, out var state));
        Assert.Equal(0, state.CurrentFrameIndex);
        Assert.Equal(0f, state.ElapsedTime);
    }

    [Fact]
    public void Play_SameLoopingStateWithoutRestartOnReplay_DoesNotResetFrame()
    {
        var (world, entity, system) = MakeMachine();
        system.Update(0f);
        world.AddComponent(entity, new AnimationState(CurrentFrameIndex: 1, ElapsedTime: 0.05f));

        system.Play(entity, "idle");
        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationState>(entity, out var state));
        Assert.Equal(1, state.CurrentFrameIndex);
        Assert.Equal(0.05f, state.ElapsedTime);
    }

    [Fact]
    public void Play_SameStateWithRestartOnReplay_ResetsFrame()
    {
        var (world, entity, system) = MakeMachine(restartOnReplay: true);
        system.Update(0f);
        world.AddComponent(entity, new AnimationState(CurrentFrameIndex: 1, ElapsedTime: 0.05f));

        system.Play(entity, "idle");
        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationState>(entity, out var state));
        Assert.Equal(0, state.CurrentFrameIndex);
        Assert.Equal(0f, state.ElapsedTime);
    }

    [Fact]
    public void NonLoopingState_ReachingLastFrame_ReportsCompletionViaAnimationState()
    {
        var (world, entity, system) = MakeMachine();
        system.Update(0f);
        system.Play(entity, "jump");
        system.Update(0f);

        var animationSystem = new AnimationSystem(world);
        // Two frames of 0.1s each; drive well past the end to finish the non-looping clip.
        animationSystem.Update(1f);

        Assert.True(world.TryGetComponent<AnimationState>(entity, out var state));
        Assert.True(state.IsFinished);
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
        var world = new World();
        var entity = world.CreateEntity();
        var system = new AnimationStateMachineSystem(world);

        Assert.Throws<InvalidOperationException>(() => system.Play(entity, "idle"));
    }

    [Fact]
    public void Update_RequestedStateIsClearedAfterProcessing()
    {
        var (world, entity, system) = MakeMachine();
        system.Update(0f);

        system.Play(entity, "jump");
        system.Update(0f);

        Assert.True(world.TryGetComponent<AnimationStateMachine>(entity, out var machine));
        Assert.Null(machine.RequestedState);
    }
}
