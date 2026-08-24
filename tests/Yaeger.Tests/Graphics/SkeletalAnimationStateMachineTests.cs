using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class SkeletalAnimationStateMachineTests
{
    private static Dictionary<string, SkeletalState> States() =>
        new()
        {
            ["idle"] = new SkeletalState("idleClip"),
            ["walk"] = new SkeletalState("walkClip"),
        };

    [Fact]
    public void Constructor_ValidStates_SetsCurrentStateToInitialState()
    {
        var machine = new SkeletalAnimationStateMachine(States(), "idle");

        Assert.Equal("idle", machine.CurrentState);
    }

    [Fact]
    public void Constructor_ValidStates_DefaultsRequestedStateToNull()
    {
        var machine = new SkeletalAnimationStateMachine(States(), "idle");

        Assert.Null(machine.RequestedState);
    }

    [Fact]
    public void Constructor_ValidStates_DefaultsRestartOnReplayToFalse()
    {
        var machine = new SkeletalAnimationStateMachine(States(), "idle");

        Assert.False(machine.RestartOnReplay);
    }

    [Fact]
    public void Constructor_ValidStates_DefaultsDefaultFadeDurationToPointTwo()
    {
        var machine = new SkeletalAnimationStateMachine(States(), "idle");

        Assert.Equal(0.2f, machine.DefaultFadeDuration);
    }

    [Fact]
    public void Constructor_ValidStates_DefaultsSpeedThresholdsToNull()
    {
        var machine = new SkeletalAnimationStateMachine(States(), "idle");

        Assert.Null(machine.SpeedThresholds);
    }

    [Fact]
    public void Constructor_RestartOnReplayTrue_IsHonoured()
    {
        var machine = new SkeletalAnimationStateMachine(States(), "idle", restartOnReplay: true);

        Assert.True(machine.RestartOnReplay);
    }

    [Fact]
    public void Constructor_NullStates_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SkeletalAnimationStateMachine(null!, "idle")
        );
    }

    [Fact]
    public void Constructor_EmptyStates_ThrowsArgumentException()
    {
        var states = new Dictionary<string, SkeletalState>();

        Assert.Throws<ArgumentException>(() => new SkeletalAnimationStateMachine(states, "idle"));
    }

    [Fact]
    public void Constructor_InitialStateNotInStates_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SkeletalAnimationStateMachine(States(), "run"));
    }

    [Fact]
    public void Constructor_CopiesStatesDictionary_LaterMutationHasNoEffect()
    {
        var states = States();

        var machine = new SkeletalAnimationStateMachine(states, "idle");
        states["run"] = new SkeletalState("runClip");

        Assert.False(machine.States.ContainsKey("run"));
    }

    [Fact]
    public void Constructor_NegativeDefaultFadeDuration_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SkeletalAnimationStateMachine(States(), "idle", defaultFadeDuration: -1f)
        );
    }

    [Fact]
    public void Constructor_NegativeSpeedHysteresis_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SkeletalAnimationStateMachine(States(), "idle", speedHysteresis: -1f)
        );
    }

    [Fact]
    public void Constructor_SpeedThresholdStateNotInStates_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SkeletalAnimationStateMachine(
                States(),
                "idle",
                speedThresholds: [new SpeedThreshold("run", 5f)]
            )
        );
    }

    [Fact]
    public void Constructor_SpeedThresholdNegativeMinSpeed_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SkeletalAnimationStateMachine(
                States(),
                "idle",
                speedThresholds: [new SpeedThreshold("walk", -1f)]
            )
        );
    }

    [Fact]
    public void Constructor_ValidSpeedThresholds_AreStored()
    {
        SpeedThreshold[] thresholds = [new("idle", 0f), new("walk", 2f)];

        var machine = new SkeletalAnimationStateMachine(
            States(),
            "idle",
            speedThresholds: thresholds
        );

        Assert.Equal(thresholds, machine.SpeedThresholds);
    }
}
