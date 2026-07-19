using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Switches an <see cref="AnimationStateMachine"/> entity's active <see cref="Animation"/>/
/// <see cref="AnimationState"/> in response to <see cref="Play"/> requests.
/// </summary>
/// <remarks>
/// Run this system <i>before</i> <see cref="AnimationSystem"/> each frame, so a state switch
/// requested this frame takes effect immediately — the freshly-swapped clip is what
/// <see cref="AnimationSystem"/> then advances, rather than advancing the old one for one more
/// step first.
/// </remarks>
public class AnimationStateMachineSystem(World world)
{
    /// <summary>
    /// Requests that <paramref name="entity"/>'s state machine switch to
    /// <paramref name="stateName"/>, taking effect on the next <see cref="Update"/> call.
    /// Switching to a different state always restarts it at frame 0; switching to the state
    /// that's already active only restarts it if <see cref="AnimationStateMachine.RestartOnReplay"/>
    /// is set.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="entity"/> has no <see cref="AnimationStateMachine"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stateName"/> is not one of the state machine's states.
    /// </exception>
    public void Play(Entity entity, string stateName)
    {
        if (!world.TryGetComponent<AnimationStateMachine>(entity, out var machine))
            throw new InvalidOperationException(
                "Entity does not have an AnimationStateMachine component."
            );

        if (!machine.States.ContainsKey(stateName))
            throw new ArgumentException(
                $"State '{stateName}' is not defined for this entity's AnimationStateMachine.",
                nameof(stateName)
            );

        machine.RequestedState = stateName;
        world.AddComponent(entity, machine);
    }

    /// <summary>
    /// Applies any pending <see cref="Play"/> request, and bootstraps the initial state's clip
    /// for a state machine that hasn't rendered a frame yet.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Snapshot before mutating components on these same entities, so we never depend on
        // subtle Dictionary-enumeration-during-mutation semantics.
        var machines = world.GetStore<AnimationStateMachine>().All().ToList();

        foreach (var (entity, machineSnapshot) in machines)
        {
            var machine = machineSnapshot;
            var hasClip = world.TryGetComponent<Animation>(entity, out _);

            var requested = machine.RequestedState;
            var isReplay = requested is not null && requested == machine.CurrentState;
            var shouldSwitch =
                !hasClip || (requested is not null && (!isReplay || machine.RestartOnReplay));

            if (requested is not null)
            {
                machine.CurrentState = requested;
                machine.RequestedState = null;
            }

            if (shouldSwitch)
            {
                var clip = machine.States[machine.CurrentState];
                world.AddComponent(entity, clip);
                world.AddComponent(entity, new AnimationState());
            }

            world.AddComponent(entity, machine);
        }
    }
}
