using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class AnimationStateMachineTests
{
    private static Animation MakeClip(bool loop = true) =>
        new([new AnimationFrame("frame0.png", 0.1f)], loop);

    [Fact]
    public void Constructor_ValidStates_SetsCurrentStateToInitialState()
    {
        var states = new Dictionary<string, Animation> { ["idle"] = MakeClip() };

        var machine = new AnimationStateMachine(states, "idle");

        Assert.Equal("idle", machine.CurrentState);
    }

    [Fact]
    public void Constructor_ValidStates_DefaultsRequestedStateToNull()
    {
        var states = new Dictionary<string, Animation> { ["idle"] = MakeClip() };

        var machine = new AnimationStateMachine(states, "idle");

        Assert.Null(machine.RequestedState);
    }

    [Fact]
    public void Constructor_ValidStates_DefaultsRestartOnReplayToFalse()
    {
        var states = new Dictionary<string, Animation> { ["idle"] = MakeClip() };

        var machine = new AnimationStateMachine(states, "idle");

        Assert.False(machine.RestartOnReplay);
    }

    [Fact]
    public void Constructor_RestartOnReplayTrue_IsHonoured()
    {
        var states = new Dictionary<string, Animation> { ["idle"] = MakeClip() };

        var machine = new AnimationStateMachine(states, "idle", restartOnReplay: true);

        Assert.True(machine.RestartOnReplay);
    }

    [Fact]
    public void Constructor_NullStates_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AnimationStateMachine(null!, "idle"));
    }

    [Fact]
    public void Constructor_EmptyStates_ThrowsArgumentException()
    {
        var states = new Dictionary<string, Animation>();

        Assert.Throws<ArgumentException>(() => new AnimationStateMachine(states, "idle"));
    }

    [Fact]
    public void Constructor_InitialStateNotInStates_ThrowsArgumentException()
    {
        var states = new Dictionary<string, Animation> { ["idle"] = MakeClip() };

        Assert.Throws<ArgumentException>(() => new AnimationStateMachine(states, "run"));
    }

    [Fact]
    public void Constructor_CopiesStatesDictionary_LaterMutationHasNoEffect()
    {
        var states = new Dictionary<string, Animation> { ["idle"] = MakeClip() };

        var machine = new AnimationStateMachine(states, "idle");
        states["run"] = MakeClip();

        Assert.False(machine.States.ContainsKey("run"));
    }
}
