namespace Yaeger.Sequencing;

/// <summary>
/// Opaque handle to a sequence started via <see cref="Systems.SequenceSystem.Play"/>.
/// </summary>
/// <remarks>
/// Sequences are system-owned and keyed by handle rather than represented as ECS components: a
/// step list holding delegates (tween starters, predicates, callbacks) can't satisfy
/// <c>ComponentStorage&lt;T&gt;</c>'s struct constraint, so <see cref="Systems.SequenceSystem"/>
/// keeps running sequences in an internal dictionary, the same way <c>ParticleSystem</c> owns its
/// emitter pools and <c>AudioSystem</c> owns its OpenAL sources.
/// </remarks>
public readonly record struct SequenceHandle(int Id);
