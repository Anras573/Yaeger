using Yaeger.Sequencing;

namespace Yaeger.Systems;

/// <summary>
/// Drives one or more running <see cref="Sequence"/>s: ordered (and optionally parallel) steps —
/// waits, tween/skeletal-clip starters, predicates, callbacks — advanced each frame with no
/// per-frame timer math in game code. Lives in <c>Yaeger.Core</c> — no <c>Window</c>/GL dependency,
/// same as <see cref="TransformHierarchySystem"/> and <see cref="TweenSystem"/>. See docs/sequencing.md.
/// </summary>
/// <remarks>
/// Running sequences are kept in an internal dictionary keyed by <see cref="SequenceHandle"/> —
/// not ECS components, since a step list holding delegates can't satisfy
/// <c>ComponentStorage&lt;T&gt;</c>'s struct constraint (see <see cref="SequenceHandle"/>'s remarks).
/// A finished sequence's entry is kept (not removed) so <see cref="TryGetStatus"/>/
/// <see cref="IsFinished"/> keep answering correctly for a handle the caller is still polling —
/// there's no unbounded growth risk here the way there is for per-frame-spawned particles or audio
/// sources, since sequences are typically few and long-lived (a cutscene, not a projectile).
/// </remarks>
public sealed class SequenceSystem : IUpdateSystem
{
    private readonly Dictionary<SequenceHandle, RunningSequence> _sequences = [];
    private int _nextHandleId = 1;

    /// <summary>
    /// Starts <paramref name="sequence"/> running and returns a handle for
    /// <see cref="Pause"/>/<see cref="Resume"/>/<see cref="Stop"/>/<see cref="SkipToEnd"/>. A
    /// single built <see cref="Sequence"/> can be played more than once concurrently — each call
    /// gets its own fresh run-state cursor.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="sequence"/> is <c>null</c>.</exception>
    public SequenceHandle Play(Sequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        var handle = new SequenceHandle(_nextHandleId++);
        _sequences[handle] = new RunningSequence(new StepCursor(sequence.Root));
        return handle;
    }

    /// <summary>Freezes <paramref name="handle"/> in place; <see cref="Update"/> skips it until <see cref="Resume"/>. No-op for an unknown, paused, or finished handle.</summary>
    public void Pause(SequenceHandle handle)
    {
        if (
            _sequences.TryGetValue(handle, out var running)
            && running.Status == SequenceStatus.Running
        )
            running.Status = SequenceStatus.Paused;
    }

    /// <summary>Un-freezes a handle previously <see cref="Pause"/>d. No-op for an unknown, running, or finished handle.</summary>
    public void Resume(SequenceHandle handle)
    {
        if (
            _sequences.TryGetValue(handle, out var running)
            && running.Status == SequenceStatus.Paused
        )
            running.Status = SequenceStatus.Running;
    }

    /// <summary>
    /// Stops <paramref name="handle"/> immediately, wherever it is — no further steps run, and no
    /// pending action fires. Terminal: marks the sequence <see cref="SequenceStatus.Finished"/>.
    /// No-op for an unknown or already-finished handle. Prefer <see cref="SkipToEnd"/> when the
    /// remaining steps' side effects (e.g. a door ending up open) still need to happen, not just
    /// an immediate halt.
    /// </summary>
    public void Stop(SequenceHandle handle)
    {
        if (_sequences.TryGetValue(handle, out var running))
            running.Status = SequenceStatus.Finished;
    }

    /// <summary>
    /// Fast-forwards <paramref name="handle"/> through every remaining step synchronously: waits
    /// (for seconds or a predicate) are bypassed without being evaluated, but every not-yet-run
    /// <see cref="Sequencing.SequenceBuilder.StartTween"/>/
    /// <see cref="Sequencing.SequenceBuilder.StartSkeletalClip"/>/
    /// <see cref="Sequencing.SequenceBuilder.Callback"/> action still fires, in order, so the
    /// sequence's end state is exactly as if it had played out normally.
    /// Terminal: marks the sequence <see cref="SequenceStatus.Finished"/>. No-op for an unknown or
    /// already-finished handle.
    /// </summary>
    public void SkipToEnd(SequenceHandle handle)
    {
        if (
            !_sequences.TryGetValue(handle, out var running)
            || running.Status == SequenceStatus.Finished
        )
            return;

        Skip(running.Cursor);
        running.Status = SequenceStatus.Finished;
    }

    /// <summary>Looks up the current <see cref="SequenceStatus"/> of <paramref name="handle"/>.</summary>
    /// <returns><c>false</c> if <paramref name="handle"/> is unknown (never played).</returns>
    public bool TryGetStatus(SequenceHandle handle, out SequenceStatus status)
    {
        if (_sequences.TryGetValue(handle, out var running))
        {
            status = running.Status;
            return true;
        }

        status = default;
        return false;
    }

    /// <summary>
    /// Convenience over <see cref="TryGetStatus"/> for the common "poll until done" pattern,
    /// mirroring <c>Tween.IsFinished</c>/<c>AnimationState.IsFinished</c>. An unknown handle counts
    /// as finished, so a stale or never-played handle never reads as still running.
    /// </summary>
    public bool IsFinished(SequenceHandle handle) =>
        !TryGetStatus(handle, out var status) || status == SequenceStatus.Finished;

    /// <inheritdoc/>
    public void Update(float deltaTime)
    {
        // Guard against negative/non-finite deltaTime, same convention as TweenSystem/AnimationSystem.
        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            return;

        foreach (var running in _sequences.Values)
        {
            if (running.Status != SequenceStatus.Running)
                continue;

            var budget = deltaTime;
            if (Advance(running.Cursor, ref budget))
                running.Status = SequenceStatus.Finished;
        }
    }

    /// <summary>
    /// Advances <paramref name="cursor"/> by up to <paramref name="budget"/> seconds, consuming
    /// whatever it actually needs and leaving the remainder in <paramref name="budget"/> so a chain
    /// of instantly-completing steps (actions, satisfied predicates, zero-second waits) can all
    /// resolve within a single <see cref="Update"/> call instead of costing one frame each. Returns
    /// whether <paramref name="cursor"/>'s step is now complete.
    /// </summary>
    private static bool Advance(StepCursor cursor, ref float budget)
    {
        if (cursor.Done)
            return true;

        switch (cursor.Step.Kind)
        {
            case SequenceStepKind.WaitForSeconds:
            {
                var remaining = MathF.Max(cursor.Step.Duration - cursor.Elapsed, 0f);
                var consume = MathF.Min(budget, remaining);
                cursor.Elapsed += consume;
                budget -= consume;
                cursor.Done = cursor.Elapsed >= cursor.Step.Duration;
                return cursor.Done;
            }

            case SequenceStepKind.Action:
            {
                RunActionOnce(cursor);
                cursor.Done = true;
                return true;
            }

            case SequenceStepKind.WaitUntil:
            {
                if (!cursor.Step.Predicate!.Invoke())
                {
                    // Not satisfied yet: nothing else can happen on this branch this frame.
                    budget = 0f;
                    return false;
                }
                cursor.Done = true;
                return true;
            }

            case SequenceStepKind.Sequential:
            {
                cursor.Children ??= CreateChildCursors(cursor.Step);
                while (cursor.ChildIndex < cursor.Children.Count)
                {
                    if (!Advance(cursor.Children[cursor.ChildIndex], ref budget))
                        return false;
                    cursor.ChildIndex++;
                }
                cursor.Done = true;
                return true;
            }

            case SequenceStepKind.Parallel:
            {
                cursor.Children ??= CreateChildCursors(cursor.Step);
                var allDone = true;
                foreach (var child in cursor.Children)
                {
                    if (child.Done)
                        continue;
                    // Each branch runs concurrently, so each gets the full incoming budget
                    // independently rather than splitting it — a shorter branch finishing early
                    // doesn't hand its leftover time to a slower sibling.
                    var childBudget = budget;
                    if (!Advance(child, ref childBudget))
                        allDone = false;
                }

                if (!allDone)
                {
                    budget = 0f;
                    return false;
                }
                cursor.Done = true;
                return true;
            }

            default:
                cursor.Done = true;
                return true;
        }
    }

    /// <summary>
    /// Fast-forward counterpart to <see cref="Advance"/>: recursively marks every not-yet-done step
    /// complete, still firing any not-yet-run <see cref="SequenceStepKind.Action"/> so
    /// <see cref="SkipToEnd"/>'s side effects land, but never evaluating a
    /// <see cref="SequenceStepKind.WaitUntil"/> predicate or accumulating <see cref="SequenceStepKind.WaitForSeconds"/> time.
    /// </summary>
    private static void Skip(StepCursor cursor)
    {
        if (cursor.Done)
            return;

        switch (cursor.Step.Kind)
        {
            case SequenceStepKind.Action:
                RunActionOnce(cursor);
                break;

            case SequenceStepKind.Sequential:
                cursor.Children ??= CreateChildCursors(cursor.Step);
                for (; cursor.ChildIndex < cursor.Children.Count; cursor.ChildIndex++)
                    Skip(cursor.Children[cursor.ChildIndex]);
                break;

            case SequenceStepKind.Parallel:
                cursor.Children ??= CreateChildCursors(cursor.Step);
                foreach (var child in cursor.Children)
                    Skip(child);
                break;
        }

        cursor.Done = true;
    }

    private static void RunActionOnce(StepCursor cursor)
    {
        if (cursor.ActionRan)
            return;
        cursor.Step.Action?.Invoke();
        cursor.ActionRan = true;
    }

    private static List<StepCursor> CreateChildCursors(SequenceStep step) =>
        step.Children!.Select(child => new StepCursor(child)).ToList();

    /// <summary>One running <see cref="Sequence"/>'s state: its status plus the root of its run-state cursor tree.</summary>
    private sealed class RunningSequence(StepCursor cursor)
    {
        public StepCursor Cursor { get; } = cursor;
        public SequenceStatus Status { get; set; } = SequenceStatus.Running;
    }

    /// <summary>
    /// Per-playback progress for one <see cref="SequenceStep"/> node. Mutable, unlike
    /// <see cref="SequenceStep"/> itself, which stays a shared, reusable definition.
    /// </summary>
    private sealed class StepCursor(SequenceStep step)
    {
        public SequenceStep Step { get; } = step;
        public float Elapsed;
        public bool ActionRan;
        public bool Done;
        public int ChildIndex;
        public List<StepCursor>? Children;
    }
}
