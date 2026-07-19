# 2D Physics

`PhysicsWorld2D` is the facade for Yaeger's 2D physics: gravity, movement integration,
spatial-hash broadphase, AABB/circle narrowphase, and impulse-based resolution with
positional correction. Construct it with a `World` and step it once per frame:

```csharp
var physics = new PhysicsWorld2D(world, gravity: new Vector2(0, -9.81f));

window.OnUpdate += delta => physics.Update((float)delta);
```

See `CLAUDE.md`'s Physics section for the full component/system rundown (colliders, triggers,
layers/masks, one-way platforms, tilemap collision, `CharacterController2D`). This page covers
two cross-cutting concerns: fixed-timestep stepping and tunneling prevention.

## Fixed-timestep stepping

`Update(deltaTime)` doesn't feed `deltaTime` straight into the simulation. Instead it runs a
classic accumulator:

1. `deltaTime` is added to a running total.
2. While that total holds at least one whole `FixedTimeStep` (default 1/120s), the world
   advances by exactly one `FixedTimeStep` and the total is reduced accordingly.
3. Whatever remains (less than one `FixedTimeStep`) carries over to the next `Update` call.

Callers don't change anything about how they call `Update` — pass your frame's own delta, every
frame, same as always. What changes is that the simulation itself always advances in fixed
increments, so two runs of the same scenario fed different frame rates (e.g. 30 fps vs 240 fps)
produce the same sequence of physics steps and the same result, within float tolerance. This is
what makes jump heights, fall speeds, and collision timing frame-rate-independent.

`InterpolationAlpha` exposes how far into the *next*, not-yet-run step the accumulator sits, as
a fraction in `[0, 1)`. `PhysicsWorld2D` doesn't use it internally — it's there for render-side
interpolation between the previous and current physics state, which is the caller's
responsibility (e.g. blend `Transform2D` positions by `Alpha` if you want silky-smooth rendering
at a higher frame rate than the physics step).

### Spiral-of-death guard

If a frame takes an unusually long time (a debugger pause, a huge hitch), the accumulator could
in principle demand a huge burst of catch-up steps — each of which takes time to run, which
delays the next frame, accumulating an ever-growing backlog. `MaxSubSteps` (default 8) caps how
many sub-steps a single `Update` call will run; any accumulated time beyond
`MaxSubSteps * FixedTimeStep` is discarded rather than carried forward. The simulation falls
behind wall-clock time during a sustained hitch instead of trying to fully catch up and making
the hitch worse.

### The zero-delta special case

A `deltaTime` of zero or less runs exactly one step immediately with that raw value, bypassing
the accumulator entirely — there's no future instant for a non-positive delta to accumulate
toward. This keeps single-step call sites (tests that want detection/resolution/events to run
once without elapsing simulated time, or manual single-step invocations) deterministic
regardless of whatever leftover the accumulator is currently holding.

## Tunneling prevention

Collision detection is discrete: it runs *after* movement integration, on whatever position the
body ends up at. A body moving fast enough can, in principle, integrate straight past a thin
obstacle in one step without the two AABBs ever overlapping at a sampled instant — the classic
"tunneling" bug, and a real risk for a fast-falling platformer character landing on a
one-tile-thick platform.

Fixed-timestep sub-stepping alone already narrows the window a lot: at 120 Hz, a given velocity
covers a much smaller distance per step than it would at a low frame rate's single large step.
For everyday platformer speeds this is normally enough. For anything faster, `MovementSystem`
additionally sweeps each dynamic body's planned displacement against solid obstacles before
committing it:

- Only `BoxCollider2D` vs `BoxCollider2D` is covered (no circles) — box-vs-box is the platformer
  case (platforms, walls, tilemap-generated colliders).
- Only dynamic movers are swept, and only against obstacles that are *not* themselves dynamic
  (static bodies, kinematic bodies, or colliders with no `RigidBody2D` at all, which default to
  static). Dynamic-vs-dynamic tunneling is out of scope.
- Triggers and one-way platforms are excluded from the sweep. A trigger isn't meant to block
  movement in the first place; a one-way platform's pass-through direction depends on approach
  velocity in a way that doesn't reduce cleanly to a single swept contact, so it's left to
  discrete detection only (and can itself still be tunneled through by an extreme enough
  velocity — a known limitation).
- Respects the same layer/mask filtering as the rest of the physics pipeline.

The sweep (`SweptAabb`, a Minkowski-sum ray-vs-box test) finds the fraction of the planned
displacement at which the mover would first touch an obstacle. When that fraction is less than
1 (i.e. the naive displacement would pass through it), the actual displacement applied this step
is clamped to just past that contact point — a small fixed overshoot (0.001 world units, not a
fraction of the full displacement, so an extreme velocity can't itself overshoot past a thin
obstacle's far side) so this step's discrete `CollisionDetectionSystem` pass finds a small
genuine overlap and `CollisionResolutionSystem` resolves it normally (impulse, restitution,
friction, positional correction) — instead of the body resting exactly at zero penetration with
its velocity untouched.

Note that only *position* is clamped; velocity is left alone. The actual bounce/stop/slide
response comes from the same step's ordinary discrete resolution, which runs immediately
afterward in the same `PhysicsWorld2D` step — not from the sweep itself.

Candidate obstacles are queried brute-force each `MovementSystem.Update` call (no broadphase) —
adequate for typical level sizes; revisit if profiling shows otherwise.
