using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Sequencing;

/// <summary>
/// Fluent builder for assembling a <see cref="Sequence"/> in game code, in the spirit of
/// <see cref="Yaeger.ECS.PrefabBuilder"/>/<see cref="Yaeger.UI.UiBuilder"/>. Captures
/// <paramref name="world"/> so <see cref="StartTween"/>/<see cref="StartSkeletalClip"/>/
/// <see cref="WaitForTweenFinished"/> don't need it threaded through every call site.
/// </summary>
/// <remarks>
/// Every builder method compiles down to one of the handful of <see cref="SequenceStepKind"/>
/// primitives <c>Systems.SequenceSystem</c> actually executes: <see cref="StartTween"/>,
/// <see cref="StartSkeletalClip"/>, and <see cref="Callback"/> are all a one-shot
/// <see cref="SequenceStepKind.Action"/>; <see cref="WaitForTweenFinished"/> is
/// <see cref="SequenceStepKind.WaitUntil"/> with a built-in predicate. Steps run in the order
/// they're added; <see cref="Parallel"/> is the only way to run more than one branch concurrently.
/// </remarks>
/// <example>
/// <code>
/// var sequence = new SequenceBuilder(world)
///     .StartTween(lift, Tween.Create(lift, TweenChannel.Transform3DPosition, top, bottom, 3f))
///     .WaitForTweenFinished(lift)
///     .WaitForSeconds(0.5f)
///     .Parallel(
///         branch => branch.StartTween(leftDoor, openLeft).WaitForTweenFinished(leftDoor),
///         branch => branch.StartTween(rightDoor, openRight).WaitForTweenFinished(rightDoor)
///     )
///     .Callback(() => Console.WriteLine("Reveal!"))
///     .Build();
///
/// var handle = sequenceSystem.Play(sequence);
/// </code>
/// </example>
public sealed class SequenceBuilder(World world)
{
    private readonly List<SequenceStep> _steps = [];

    /// <summary>Waits <paramref name="seconds"/> before advancing. Zero is a valid no-op barrier.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is negative, <c>NaN</c>, or infinite.</exception>
    public SequenceBuilder WaitForSeconds(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(seconds),
                "Wait duration must be a non-negative, finite number of seconds."
            );

        _steps.Add(new SequenceStep(SequenceStepKind.WaitForSeconds) { Duration = seconds });
        return this;
    }

    /// <summary>
    /// Starts <paramref name="tween"/> by adding it to <paramref name="carrier"/> (the same
    /// carrier-entity pattern <see cref="Tween"/> itself uses — see docs/tweening.md). Completes
    /// the instant it runs; pair with <see cref="WaitForTweenFinished"/> to block on it.
    /// </summary>
    public SequenceBuilder StartTween(Entity carrier, Tween tween)
    {
        _steps.Add(
            new SequenceStep(SequenceStepKind.Action)
            {
                Action = () => world.AddComponent(carrier, tween),
            }
        );
        return this;
    }

    /// <summary>
    /// Starts a skeletal clip on <paramref name="entity"/> by replacing its
    /// <see cref="AnimationPlayer"/> outright (same hard-switch semantics as assigning
    /// <see cref="AnimationPlayer.CurrentClip"/> directly — any in-progress crossfade is dropped).
    /// <paramref name="entity"/> must already carry a <c>SkeletonHandle</c>; that part of setup is
    /// the caller's responsibility, same as for any other <see cref="AnimationPlayer"/> entity.
    /// </summary>
    public SequenceBuilder StartSkeletalClip(
        Entity entity,
        string? clipName,
        bool loop = false,
        float speed = 1f
    )
    {
        _steps.Add(
            new SequenceStep(SequenceStepKind.Action)
            {
                Action = () =>
                    world.AddComponent(entity, new AnimationPlayer(clipName, loop, speed)),
            }
        );
        return this;
    }

    /// <summary>
    /// Waits until the <see cref="Tween"/> component on <paramref name="carrier"/> reports
    /// <see cref="Tween.IsFinished"/>. Fails safe rather than hanging: if <paramref name="carrier"/>
    /// (or its <see cref="Tween"/>) is gone by the time this step runs — the entity was destroyed,
    /// or the tween never existed — it's treated as already finished instead of waiting forever.
    /// A <see cref="TweenLoopMode.Loop"/>/<see cref="TweenLoopMode.PingPong"/> tween never sets
    /// <c>IsFinished</c>, so waiting on one here would otherwise hang for the sequence's lifetime.
    /// </summary>
    public SequenceBuilder WaitForTweenFinished(Entity carrier)
    {
        _steps.Add(
            new SequenceStep(SequenceStepKind.WaitUntil)
            {
                Predicate = () =>
                    !world.TryGetComponent<Tween>(carrier, out var tween) || tween.IsFinished,
            }
        );
        return this;
    }

    /// <summary>
    /// Waits until <paramref name="predicate"/> returns <c>true</c>, polled once per
    /// <c>Systems.SequenceSystem.Update</c> call. The general escape hatch for conditions the
    /// other step kinds don't name directly — for example, waiting on a skeletal clip to finish
    /// (no dedicated step yet; tracked separately) by comparing <see cref="AnimationPlayer.Time"/>
    /// against a known clip duration.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    public SequenceBuilder WaitUntil(Func<bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _steps.Add(new SequenceStep(SequenceStepKind.WaitUntil) { Predicate = predicate });
        return this;
    }

    /// <summary>Invokes <paramref name="callback"/> once, then advances immediately.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <c>null</c>.</exception>
    public SequenceBuilder Callback(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _steps.Add(new SequenceStep(SequenceStepKind.Action) { Action = callback });
        return this;
    }

    /// <summary>
    /// Runs each <paramref name="branches"/> entry as its own ordered sub-sequence, all
    /// concurrently; completes once every branch has. Each branch gets a fresh
    /// <see cref="SequenceBuilder"/> (sharing this builder's <see cref="World"/>) to assemble its
    /// own steps on.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="branches"/> is <c>null</c>.</exception>
    public SequenceBuilder Parallel(params Action<SequenceBuilder>[] branches)
    {
        ArgumentNullException.ThrowIfNull(branches);

        var children = new List<SequenceStep>(branches.Length);
        foreach (var branch in branches)
        {
            var nested = new SequenceBuilder(world);
            branch(nested);
            children.Add(
                new SequenceStep(SequenceStepKind.Sequential) { Children = nested._steps }
            );
        }

        _steps.Add(new SequenceStep(SequenceStepKind.Parallel) { Children = children });
        return this;
    }

    /// <summary>Builds the immutable <see cref="Sequence"/> from the steps added so far.</summary>
    public Sequence Build() =>
        new(new SequenceStep(SequenceStepKind.Sequential) { Children = _steps.ToList() });
}
