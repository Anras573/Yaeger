namespace Yaeger.Sequencing;

/// <summary>
/// An immutable, built step tree produced by <see cref="SequenceBuilder.Build"/> and run by
/// <see cref="Systems.SequenceSystem"/>. A single built <see cref="Sequence"/> can be
/// <see cref="Systems.SequenceSystem.Play"/>ed more than once — each call creates a fresh run-state
/// cursor, so concurrent playbacks of the same definition never share progress.
/// </summary>
public sealed class Sequence
{
    internal SequenceStep Root { get; }

    internal Sequence(SequenceStep root) => Root = root;
}
