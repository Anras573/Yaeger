# Sequencing

`SequenceSystem` runs an ordered list of timed steps — "do this, wait for it to finish, then do
that" — so a directed beat (a lift descending, then settling, then its doors opening, then a
reveal) can be authored as data instead of a hand-rolled state machine with float timers in
`OnUpdate`. [Tweens](tweening.md) give the *what*; sequences give the *when*.

## Quick start

```csharp
var sequenceSystem = new SequenceSystem();

var sequence = new SequenceBuilder(world)
    .StartTween(lift, Tween.Create(lift, TweenChannel.Transform3DPosition, top, bottom, 3f))
    .WaitForTweenFinished(lift)
    .WaitForSeconds(0.5f)
    .Callback(() => Console.WriteLine("Doors opening"))
    .Build();

var handle = sequenceSystem.Play(sequence);

window.OnUpdate += dt => sequenceSystem.Update((float)dt);
```

`SequenceSystem` implements `IUpdateSystem` and lives in `Yaeger.Core` (no `Window`/GL dependency,
same as `TransformHierarchySystem`/`TweenSystem`), so it works in headless tests as well as native
games. See `Samples/SequenceDemo` for a complete multi-beat sequence.

## Building a sequence: `SequenceBuilder`

`SequenceBuilder` is a fluent builder in the spirit of `PrefabBuilder`/`UiBuilder`. Construct it
with the `World` the sequence's entities live in, chain step methods, then call `Build()` to get an
immutable `Sequence`. A single built `Sequence` can be `Play`ed more than once — each call gets its
own fresh progress, so concurrent playbacks of the same definition never interfere.

| Method | What it does |
|---|---|
| `WaitForSeconds(seconds)` | Waits `seconds` before advancing. `0` is a valid no-op barrier. |
| `StartTween(carrier, tween)` | Adds `tween` to `carrier` (the same carrier-entity pattern `Tween` itself uses — see [tweening.md](tweening.md)). Completes the instant it runs. |
| `StartSkeletalClip(entity, clipName, loop, speed)` | Replaces `entity`'s `AnimationPlayer` outright — a hard switch, like assigning `CurrentClip` directly. `entity` must already carry a `SkeletonHandle`. |
| `WaitForTweenFinished(carrier)` | Waits until the `Tween` on `carrier` reports `IsFinished`. |
| `WaitUntil(predicate)` | Waits until an arbitrary `Func<bool>` returns `true`, polled once per `Update` call. |
| `Callback(action)` | Invokes `action` once, then advances immediately. |
| `Parallel(branch1, branch2, ...)` | Runs each branch (its own ordered sub-sequence, built with a nested `SequenceBuilder`) concurrently; completes once every branch does. |

Steps run in the order they're added. `Parallel` is the only way to run more than one branch
concurrently; a branch inside `Parallel` can itself contain a nested `Parallel`.

```csharp
var sequence = new SequenceBuilder(world)
    .StartTween(lift, liftDown)
    .WaitForTweenFinished(lift)
    .Parallel(
        branch => branch.StartTween(leftDoor, openLeft).WaitForTweenFinished(leftDoor),
        branch => branch.StartTween(rightDoor, openRight).WaitForTweenFinished(rightDoor)
    )
    .Callback(() => reveal.Begin())
    .Build();
```

## Playing a sequence: `SequenceSystem`

Sequences are **system-owned and keyed by handle, not ECS components** — a step list holding
delegates (tween starters, predicates, callbacks) can't satisfy `ComponentStorage<T>`'s struct
constraint, the same reason `ParticleSystem` owns its emitter pools and `AudioSystem` owns its
OpenAL sources internally rather than as components.

```csharp
var handle = sequenceSystem.Play(sequence);

sequenceSystem.Pause(handle);
sequenceSystem.Resume(handle);
sequenceSystem.Stop(handle);       // halts immediately, wherever it is
sequenceSystem.SkipToEnd(handle);  // fast-forwards — see below

if (sequenceSystem.IsFinished(handle)) { /* ... */ }
sequenceSystem.TryGetStatus(handle, out var status); // Running / Paused / Finished
```

`Pause`/`Resume`/`Stop`/`SkipToEnd` are no-ops for an unknown or already-finished handle, so calling
them speculatively (e.g. on a handle a UI button remembers) never throws. `IsFinished` also returns
`true` for a handle that was never played, so a stale or default `SequenceHandle` reads as "nothing
to wait for" rather than "still running forever".

### Skip-to-end

`SkipToEnd` matters a lot for iteration — nobody wants to sit through a 20-second lift ride on every
test run. It fast-forwards synchronously through every remaining step: `WaitForSeconds` and
`WaitUntil` are bypassed without their duration/predicate ever being evaluated, but every
not-yet-run `StartTween`/`StartSkeletalClip`/`Callback` action still fires, in order — including
every branch of a `Parallel` group — so the sequence's end state (doors open, lift at the bottom,
callbacks fired) is exactly as if it had played out normally. An action that already ran before
`SkipToEnd` was called never fires a second time.

`Stop` is the blunter tool: it halts immediately, wherever the sequence currently is, and no further
actions run at all — reach for `SkipToEnd` instead when the remaining steps' side effects still need
to happen.

## Frame-rate independence

A single large `deltaTime` (a stall, or a test driving `Update` with big fixed steps) still walks
through every step it has time for in one call, not just the first: completing a step carries its
leftover time-budget forward into the next step in the same `Sequential` list, so a chain of
zero-duration waits, callbacks, and already-satisfied predicates all resolve within one frame
instead of costing a frame each. Inside a `Parallel` group, each branch gets the **full** incoming
budget independently rather than a split share — a branch finishing early doesn't hand its leftover
time to a slower sibling, and a slower branch still finishes on its own schedule rather than falling
further behind every frame.

## Fail-safe: entities destroyed mid-sequence

A sequence that outlives its entities fails safe instead of hanging or throwing.
`WaitForTweenFinished(carrier)` is implemented as `!world.TryGetComponent<Tween>(carrier, out var
tween) || tween.IsFinished` — if `carrier` (or its `Tween`) is gone by the time the step runs, the
wait is treated as already satisfied and the sequence proceeds, rather than waiting forever for a
tween that will never report finished. `StartTween`/`StartSkeletalClip` add components via
`World.AddComponent`, which never throws for a destroyed entity id. A `WaitUntil` predicate you
write yourself is your own code, though — if it dereferences a destroyed entity's component with
`World.GetComponent` (which throws on a miss) rather than `TryGetComponent`, that's the same
footgun it would be anywhere else in the engine.

## Known limitations

- **Waiting on a skeletal clip finishing has no dedicated step yet.** `AnimationPlayer` doesn't
  expose a completion flag the way `Tween.IsFinished`/`AnimationState.IsFinished` do (tracked
  separately). Until it does, use `WaitUntil(...)` and compare `AnimationPlayer.Time` against a
  known clip duration yourself.
- No JSON/DSL cutscene file format — sequences are authored in code via `SequenceBuilder` only.
- No branching, conditional graphs, or behaviour trees — steps are a flat ordered/parallel tree.
- No rewind or scrubbing backwards; a sequence only ever moves forward (or is skipped to its end).
