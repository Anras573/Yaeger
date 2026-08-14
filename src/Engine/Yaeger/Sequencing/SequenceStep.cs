namespace Yaeger.Sequencing;

/// <summary>
/// One node of a built <see cref="Sequence"/>'s step tree. Immutable and definition-only — all
/// per-playback progress (elapsed time, which child is active, whether <see cref="Action"/> has
/// run) lives in <c>Systems.SequenceSystem</c>'s own run-state cursor, so the same
/// <see cref="SequenceStep"/> tree can be played concurrently by multiple
/// <see cref="Systems.SequenceSystem.Play"/> calls without shared mutable state.
/// </summary>
internal sealed class SequenceStep(SequenceStepKind kind)
{
    public SequenceStepKind Kind { get; } = kind;

    /// <summary>Seconds to wait. Only meaningful for <see cref="SequenceStepKind.WaitForSeconds"/>.</summary>
    public float Duration { get; init; }

    /// <summary>Runs once. Only meaningful for <see cref="SequenceStepKind.Action"/>.</summary>
    public Action? Action { get; init; }

    /// <summary>Polled until <c>true</c>. Only meaningful for <see cref="SequenceStepKind.WaitUntil"/>.</summary>
    public Func<bool>? Predicate { get; init; }

    /// <summary>
    /// Child steps. For <see cref="SequenceStepKind.Sequential"/>, run one at a time in order; for
    /// <see cref="SequenceStepKind.Parallel"/>, each entry is itself a <see cref="SequenceStepKind.Sequential"/>
    /// branch run concurrently with the others.
    /// </summary>
    public IReadOnlyList<SequenceStep>? Children { get; init; }
}
