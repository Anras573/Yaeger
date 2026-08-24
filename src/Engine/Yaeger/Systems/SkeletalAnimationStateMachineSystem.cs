using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Switches a <see cref="SkeletalAnimationStateMachine"/> entity's active clip in response to
/// <see cref="Play"/> requests and, when configured, locomotion speed — the 3D analogue of
/// <see cref="AnimationStateMachineSystem"/>, driving <see cref="SkeletalAnimationSystem.CrossFadeTo"/>
/// instead of swapping <see cref="Animation"/>/<see cref="AnimationState"/> components.
/// </summary>
/// <remarks>
/// Run this system <i>after</i> whatever moves the entity (typically <c>PathFollow3DSystem</c>, so a
/// same-frame speed change is seen this frame) and <i>before</i> <see cref="SkeletalAnimationSystem"/>,
/// so a switch requested this frame is what gets sampled this frame rather than one frame later.
/// </remarks>
public sealed class SkeletalAnimationStateMachineSystem(
    World world,
    SkeletalAnimationSystem animationSystem
)
{
    /// <summary>
    /// Requests that <paramref name="entity"/>'s state machine switch to <paramref name="stateName"/>,
    /// taking effect on the next <see cref="Update"/> call. Switching to a different state always
    /// crossfades; switching to the state that's already active only restarts it if
    /// <see cref="SkeletalAnimationStateMachine.RestartOnReplay"/> is set. This request takes
    /// priority over a speed-driven choice made the same frame.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="entity"/> has no <see cref="SkeletalAnimationStateMachine"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stateName"/> is not one of the state machine's states.
    /// </exception>
    public void Play(Entity entity, string stateName)
    {
        if (!world.TryGetComponent<SkeletalAnimationStateMachine>(entity, out var machine))
            throw new InvalidOperationException(
                "Entity does not have a SkeletalAnimationStateMachine component."
            );

        if (!machine.States.ContainsKey(stateName))
            throw new ArgumentException(
                $"State '{stateName}' is not defined for this entity's SkeletalAnimationStateMachine.",
                nameof(stateName)
            );

        machine.RequestedState = stateName;
        world.AddComponent(entity, machine);
    }

    /// <summary>
    /// Applies any pending <see cref="Play"/> request (or, absent one, a speed-driven request derived
    /// from the entity's <see cref="PathFollow3D.CurrentSpeed"/> when <see cref="SkeletalAnimationStateMachine.SpeedThresholds"/>
    /// is configured), and bootstraps the initial state's clip for a state machine that hasn't
    /// rendered a frame yet.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Snapshot before mutating components on these same entities, mirroring
        // AnimationStateMachineSystem.Update.
        var machines = world.GetStore<SkeletalAnimationStateMachine>().All().ToList();

        foreach (var (entity, machineSnapshot) in machines)
        {
            var machine = machineSnapshot;

            if (
                machine.RequestedState is null
                && machine.SpeedThresholds is { Length: > 0 }
                && world.TryGetComponent<PathFollow3D>(entity, out var path)
            )
            {
                machine.RequestedState = ResolveSpeedDrivenState(machine, path.CurrentSpeed);
            }

            var hasPlayer = world.TryGetComponent<AnimationPlayer>(entity, out _);

            var requested = machine.RequestedState;
            var isReplay = requested is not null && requested == machine.CurrentState;
            var shouldSwitch =
                !hasPlayer || (requested is not null && (!isReplay || machine.RestartOnReplay));

            if (requested is not null)
            {
                machine.CurrentState = requested;
                machine.RequestedState = null;
            }

            if (shouldSwitch)
            {
                var state = machine.States[machine.CurrentState];

                if (hasPlayer)
                {
                    animationSystem.CrossFadeTo(
                        entity,
                        state.ClipName,
                        state.FadeDuration ?? machine.DefaultFadeDuration
                    );

                    // CrossFadeTo preserves the player's existing Loop value; keep it in sync with
                    // this state's own configuration (e.g. a looping "walk" switching into a
                    // non-looping "jump").
                    if (
                        world.TryGetComponent<AnimationPlayer>(entity, out var player)
                        && player.Loop != state.Loop
                    )
                    {
                        player.Loop = state.Loop;
                        world.AddComponent(entity, player);
                    }
                }
                else
                {
                    world.AddComponent(entity, new AnimationPlayer(state.ClipName, state.Loop));
                }
            }

            world.AddComponent(entity, machine);
        }
    }

    /// <summary>
    /// Picks the state <paramref name="machine"/>'s <see cref="SkeletalAnimationStateMachine.SpeedThresholds"/>
    /// select for <paramref name="speed"/>, applying <see cref="SkeletalAnimationStateMachine.SpeedHysteresis"/>
    /// to transitions away from the currently active state so a speed hovering right at a threshold
    /// doesn't flicker between the two adjacent states.
    /// </summary>
    /// <remarks>
    /// The active state holds until <paramref name="speed"/> drops below its own threshold minus the
    /// hysteresis margin; only then does it re-evaluate and potentially drop straight to whichever
    /// (possibly lower still) state naturally fits. Moving to a <i>higher</i> state is never delayed —
    /// hysteresis only guards against repeatedly stepping back down.
    /// </remarks>
    internal static string ResolveSpeedDrivenState(
        SkeletalAnimationStateMachine machine,
        float speed
    )
    {
        var thresholds = machine.SpeedThresholds!;

        var naturalIndex = 0;
        for (var i = 0; i < thresholds.Length; i++)
        {
            if (speed >= thresholds[i].MinSpeed)
                naturalIndex = i;
        }

        var currentIndex = Array.FindIndex(thresholds, t => t.State == machine.CurrentState);

        if (currentIndex < 0 || naturalIndex >= currentIndex)
            return thresholds[naturalIndex].State;

        // naturalIndex < currentIndex: a downward transition is on the table. Only take it once
        // speed has actually fallen past the active state's own threshold by the hysteresis margin.
        return speed < thresholds[currentIndex].MinSpeed - machine.SpeedHysteresis
            ? thresholds[naturalIndex].State
            : thresholds[currentIndex].State;
    }
}
