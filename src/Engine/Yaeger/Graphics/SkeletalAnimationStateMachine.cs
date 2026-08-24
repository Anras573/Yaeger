namespace Yaeger.Graphics;

/// <summary>
/// One named state in a <see cref="SkeletalAnimationStateMachine"/>: which clip it plays, whether
/// that clip loops, and (optionally) how long a crossfade into this state should take.
/// </summary>
/// <param name="ClipName">The <see cref="SkeletonRegistry"/> clip name played while this state is active.</param>
/// <param name="Loop">Whether the clip loops. Defaults to <c>true</c> — the common case for idle/walk/run.</param>
/// <param name="FadeDuration">
/// Crossfade duration in seconds used when switching <em>into</em> this state, overriding
/// <see cref="SkeletalAnimationStateMachine.DefaultFadeDuration"/>. <c>null</c> (the default) falls
/// back to the shared default instead of every state needing its own value.
/// </param>
public readonly record struct SkeletalState(
    string ClipName,
    bool Loop = true,
    float? FadeDuration = null
);

/// <summary>
/// One entry in <see cref="SkeletalAnimationStateMachine.SpeedThresholds"/>: the state to select
/// once locomotion speed reaches <see cref="MinSpeed"/>.
/// </summary>
/// <param name="State">A key in <see cref="SkeletalAnimationStateMachine.States"/>.</param>
/// <param name="MinSpeed">
/// The speed (units/second) at or above which this state becomes the natural choice. Must be
/// non-negative; entries are expected in ascending order by this value (idle at 0, then walk, then
/// run, ...).
/// </param>
public readonly record struct SpeedThreshold(string State, float MinSpeed);

/// <summary>
/// Named states mapping to skeletal animation clips for an entity carrying <see cref="SkeletonHandle"/>
/// + <see cref="AnimationPlayer"/>, switched by <see cref="Systems.SkeletalAnimationStateMachineSystem"/>
/// instead of game code manually calling <see cref="Systems.SkeletalAnimationSystem.CrossFadeTo"/>. The
/// 3D analogue of <see cref="AnimationStateMachine"/>, sized the same way — "idle / walk / run", not a
/// general animation graph: no blend trees, no transition-condition DSL.
/// </summary>
/// <remarks>
/// <para>
/// Every switch — explicit via <see cref="Systems.SkeletalAnimationStateMachineSystem.Play"/>, or
/// automatic via <see cref="SpeedThresholds"/> — goes through
/// <see cref="Systems.SkeletalAnimationSystem.CrossFadeTo"/>, so it blends instead of popping.
/// Re-requesting the state that's already active is a no-op unless <see cref="RestartOnReplay"/> is
/// set, mirroring <see cref="AnimationStateMachine.RestartOnReplay"/>'s posture.
/// </para>
/// <para>
/// <see cref="SpeedThresholds"/> is optional. When set, and the entity also carries a
/// <see cref="PathFollow3D"/>, <see cref="Systems.SkeletalAnimationStateMachineSystem.Update"/> reads
/// <see cref="PathFollow3D.CurrentSpeed"/> each frame and requests whichever threshold's state
/// matches — an explicit <see cref="Systems.SkeletalAnimationStateMachineSystem.Play"/> request made
/// the same frame always wins over the speed-driven choice. <see cref="SpeedHysteresis"/> keeps a
/// character hovering near a threshold from flickering: the active state holds until speed moves
/// <see cref="SpeedHysteresis"/> past its own threshold in the direction of a lower state, rather than
/// switching back and forth right at the boundary.
/// </para>
/// </remarks>
public struct SkeletalAnimationStateMachine
{
    /// <summary>The named states this machine can switch between.</summary>
    public Dictionary<string, SkeletalState> States;

    /// <summary>The name of the currently active state. Written by the system.</summary>
    public string CurrentState;

    /// <summary>
    /// A state switch requested via <see cref="Systems.SkeletalAnimationStateMachineSystem.Play"/>,
    /// consumed (and cleared back to <c>null</c>) on the next
    /// <see cref="Systems.SkeletalAnimationStateMachineSystem.Update"/>. Treat as read-only from
    /// game code — call <c>Play</c> instead of setting this directly.
    /// </summary>
    public string? RequestedState;

    /// <summary>
    /// Whether calling <c>Play</c> (or a speed-driven request) with the name of the state that's
    /// already active restarts it rather than leaving it playing uninterrupted. Defaults to
    /// <c>false</c> — the common case for a looping "idle"/"walk"/"run" state that shouldn't
    /// visibly restart every time it's re-confirmed.
    /// </summary>
    public bool RestartOnReplay;

    /// <summary>
    /// Crossfade duration in seconds used for a transition whose target <see cref="SkeletalState"/>
    /// doesn't specify its own <see cref="SkeletalState.FadeDuration"/>. Must be non-negative.
    /// </summary>
    public float DefaultFadeDuration;

    /// <summary>
    /// Optional speed-driven state selection. <c>null</c> or empty (the default) disables it — the
    /// machine only switches via explicit <c>Play</c> requests. See the type's remarks.
    /// </summary>
    public SpeedThreshold[]? SpeedThresholds;

    /// <summary>
    /// Hysteresis margin (units/second) applied to <see cref="SpeedThresholds"/> transitions
    /// downward, so a speed hovering right at a threshold doesn't flicker between the two adjacent
    /// states. Must be non-negative. See the type's remarks.
    /// </summary>
    public float SpeedHysteresis;

    /// <summary>
    /// Creates a state machine starting in <paramref name="initialState"/>.
    /// </summary>
    /// <param name="states">
    /// The named states this machine can switch between. Must contain at least one entry. Copied at
    /// construction, so later mutating the dictionary you passed in has no effect.
    /// </param>
    /// <param name="initialState">The state to start in. Must be a key in <paramref name="states"/>.</param>
    /// <param name="defaultFadeDuration">See <see cref="DefaultFadeDuration"/>. Defaults to <c>0.2</c> seconds.</param>
    /// <param name="restartOnReplay">See <see cref="RestartOnReplay"/>. Defaults to <c>false</c>.</param>
    /// <param name="speedThresholds">See <see cref="SpeedThresholds"/>. Defaults to <c>null</c> (disabled).</param>
    /// <param name="speedHysteresis">See <see cref="SpeedHysteresis"/>. Defaults to <c>0</c>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="states"/> is empty, <paramref name="initialState"/> is not one of
    /// its keys, <paramref name="defaultFadeDuration"/>/<paramref name="speedHysteresis"/> is
    /// negative or non-finite, or a <paramref name="speedThresholds"/> entry names a state that
    /// isn't in <paramref name="states"/> or has a negative/non-finite <see cref="SpeedThreshold.MinSpeed"/>.
    /// </exception>
    public SkeletalAnimationStateMachine(
        IReadOnlyDictionary<string, SkeletalState> states,
        string initialState,
        float defaultFadeDuration = 0.2f,
        bool restartOnReplay = false,
        SpeedThreshold[]? speedThresholds = null,
        float speedHysteresis = 0f
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
        if (!float.IsFinite(defaultFadeDuration) || defaultFadeDuration < 0f)
            throw new ArgumentException(
                "Default fade duration must be a non-negative finite value.",
                nameof(defaultFadeDuration)
            );
        if (!float.IsFinite(speedHysteresis) || speedHysteresis < 0f)
            throw new ArgumentException(
                "Speed hysteresis must be a non-negative finite value.",
                nameof(speedHysteresis)
            );
        if (speedThresholds is not null)
        {
            foreach (var threshold in speedThresholds)
            {
                if (!states.ContainsKey(threshold.State))
                    throw new ArgumentException(
                        $"Speed threshold state '{threshold.State}' is not present in the given states.",
                        nameof(speedThresholds)
                    );
                if (!float.IsFinite(threshold.MinSpeed) || threshold.MinSpeed < 0f)
                    throw new ArgumentException(
                        $"Speed threshold for state '{threshold.State}' must have a non-negative finite MinSpeed.",
                        nameof(speedThresholds)
                    );
            }
        }

        States = new Dictionary<string, SkeletalState>(states);
        CurrentState = initialState;
        RequestedState = null;
        RestartOnReplay = restartOnReplay;
        DefaultFadeDuration = defaultFadeDuration;
        SpeedThresholds = speedThresholds;
        SpeedHysteresis = speedHysteresis;
    }
}
