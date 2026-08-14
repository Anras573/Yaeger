namespace Yaeger.Sequencing;

/// <summary>
/// Runtime kind of a <see cref="SequenceStep"/>. Not exposed publicly — <see cref="SequenceBuilder"/>
/// is the authoring surface; these are the handful of primitives every builder method compiles down
/// to (see <see cref="SequenceBuilder"/>'s remarks for the mapping).
/// </summary>
internal enum SequenceStepKind
{
    /// <summary><see cref="SequenceStep.Children"/> run one at a time, in order.</summary>
    Sequential,

    /// <summary><see cref="SequenceStep.Children"/> all run concurrently; completes once every child has.</summary>
    Parallel,

    /// <summary>Completes once <see cref="SequenceStep.Duration"/> seconds have elapsed.</summary>
    WaitForSeconds,

    /// <summary>Runs <see cref="SequenceStep.Action"/> once, then completes instantly.</summary>
    Action,

    /// <summary>Completes once <see cref="SequenceStep.Predicate"/> returns <c>true</c>.</summary>
    WaitUntil,
}
