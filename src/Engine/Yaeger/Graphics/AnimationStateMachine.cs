namespace Yaeger.Graphics;

/// <summary>
/// Named states mapping to <see cref="Animation"/> clips, switched cleanly by
/// <see cref="Systems.AnimationStateMachineSystem"/> instead of game code manually swapping
/// <see cref="Animation"/>/<see cref="AnimationState"/> components. Deliberately minimal: no
/// blend trees, no transition-condition DSL — game code decides <i>when</i> to switch (via
/// <see cref="Systems.AnimationStateMachineSystem.Play"/>), this only handles switching cleanly
/// (resetting the frame timer, swapping the clip).
/// </summary>
/// <remarks>
/// Works for both plain <see cref="Sprite"/> animation (whole-texture-per-frame) and
/// <see cref="SpriteSheet"/> animation (UV-sub-region-per-frame) — a "state" is just an
/// <see cref="Animation"/> clip, the same component that already drives frame timing for either
/// rendering path. Reports completion the same way any <see cref="Animation"/> does: read
/// <see cref="AnimationState.IsFinished"/> on the entity once its current (non-looping) state's
/// clip reaches its last frame.
/// </remarks>
public struct AnimationStateMachine
{
    /// <summary>The named clips this state machine can switch between.</summary>
    public Dictionary<string, Animation> States;

    /// <summary>The name of the currently active state. Written by the system.</summary>
    public string CurrentState;

    /// <summary>
    /// A state switch requested via <see cref="Systems.AnimationStateMachineSystem.Play"/>,
    /// consumed (and cleared back to <c>null</c>) on the next
    /// <see cref="Systems.AnimationStateMachineSystem.Update"/>. Treat as read-only from game
    /// code — call <c>Play</c> instead of setting this directly.
    /// </summary>
    public string? RequestedState;

    /// <summary>
    /// Whether calling <c>Play</c> with the name of the state that's already active restarts it
    /// (frame 0) rather than leaving it playing uninterrupted. Defaults to <c>false</c> — the
    /// common case for a looping "idle"/"run" state that shouldn't visibly stutter every time
    /// game code re-confirms "yes, still idle."
    /// </summary>
    public bool RestartOnReplay;

    /// <summary>
    /// Creates a state machine starting in <paramref name="initialState"/>.
    /// </summary>
    /// <param name="states">
    /// The named clips this state machine can switch between. Must contain at least one entry.
    /// Copied at construction, so later mutating the dictionary you passed in has no effect.
    /// </param>
    /// <param name="initialState">
    /// The state to start in. Must be a key in <paramref name="states"/>.
    /// </param>
    /// <param name="restartOnReplay">See <see cref="RestartOnReplay"/>. Defaults to <c>false</c>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="states"/> is empty, or <paramref name="initialState"/> is not
    /// one of its keys.
    /// </exception>
    public AnimationStateMachine(
        IReadOnlyDictionary<string, Animation> states,
        string initialState,
        bool restartOnReplay = false
    )
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count == 0)
            throw new ArgumentException("At least one state is required.", nameof(states));
        ArgumentNullException.ThrowIfNull(initialState);
        if (!states.ContainsKey(initialState))
            throw new ArgumentException(
                $"Initial state '{initialState}' is not present in the given states.",
                nameof(initialState)
            );

        States = new Dictionary<string, Animation>(states);
        CurrentState = initialState;
        RequestedState = null;
        RestartOnReplay = restartOnReplay;
    }
}
