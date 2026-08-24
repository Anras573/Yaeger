# 3D Path Following

`PathFollow3D` + `PathFollow3DSystem` drive an entity's `Transform3D` along a polyline of
waypoints at constant world speed, turning to face the direction of travel instead of snapping.
It is the 3D counterpart of [`PlatformPath`](physics.md#platformpath-optional) — the same
waypoints/speed/loop-mode idea — with the addition of gradual facing, since a walking or flying 3D
entity usually needs to visibly turn as it moves.

## Why not just use tweens?

A patrol can be authored today as a `Sequence` of `Tween`s on the position and rotation channels
(see [tweening.md](tweening.md) and [sequencing.md](sequencing.md)) — that is the right tool for a
scripted, one-off camera move or cutscene beat. It is the wrong tool for a knight walking laps: each
leg would be a separate tween whose duration you hand-compute from its length to keep speed
constant, turning would be a second tween you have to interleave, and changing one waypoint means
recomputing everything downstream. `PathFollow3D` keeps speed constant automatically and derives
facing every step, so authoring or editing a route is just editing an array of points.

## Usage

```csharp
using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

var world = new World();
var knight = world.CreateEntity("knight");
world.AddComponent(knight, Transform3D.Identity);
world.AddComponent(
    knight,
    new PathFollow3D(
        waypoints:
        [
            new Vector3(0, 0, 0),
            new Vector3(5, 0, 0),
            new Vector3(5, 0, 5),
        ],
        speed: 2f, // units/second
        turnRate: MathF.PI, // radians/second — half a turn per second
        loopMode: TweenLoopMode.PingPong
    )
);

var pathFollowSystem = new PathFollow3DSystem(world);

// Each frame, before rendering:
pathFollowSystem.Update(deltaTime);
```

`PathFollow3D` needs nothing else on the entity beyond a `Transform3D` — unlike `PlatformPath`, it
does not pair with a `RigidBody2D`/`Velocity2D`, since 3D physics does not exist yet (3D ray
queries against `Aabb3D` bounds are the closest thing so far — see [queries.md](queries.md)).
`PathFollow3DSystem` writes `Transform3D` directly every step.

## Loop modes

`LoopMode` reuses [`TweenLoopMode`](tweening.md)'s vocabulary:

- **`Once`** — travels the path once and holds at the last waypoint; `PathFollow3D.IsFinished` is
  set, and the entity is left alone (and reports `CurrentSpeed` of zero) on later steps.
- **`Loop`** (the default) — on reaching the last waypoint, heads straight back to the first and
  repeats.
- **`PingPong`** — reverses direction at either end, walking the path back and forth.

## Constant speed across leg boundaries

A single `Update` call can carry an entity past one or more waypoints without stalling or
overshooting: the step's movement budget (`Speed * deltaTime`) is spent leg by leg, and whatever is
left over after reaching a waypoint keeps being spent on the next leg in the same call. This means
simulation results (how far along the path an entity has travelled) are independent of frame rate
and of how the path happens to be subdivided into legs — a large `deltaTime`, or a very short leg,
never leaves speed "on the table" for one frame.

## Facing

While actively advancing, `Transform3D.Rotation` is slerped towards the direction of the leg
currently being travelled, at up to `PathFollow3D.TurnRate` radians per second — never snapping.
A `TurnRate` of zero never turns (facing stays exactly as authored); a very large value effectively
snaps to face the direction of travel within a single step.

`PathFollow3D.YawOnly` (`true` by default) projects the direction of travel onto the plane
perpendicular to `PathFollow3D.Up` before facing it — the usual choice for a walking character on
flat-ish ground, so it doesn't pitch up and down over uneven waypoints. Set it to `false` for a
bird, drone, or camera rig that should also pitch to match a path that climbs or dives. When the
direction of travel is exactly parallel to `Up` (straight up or down) with `YawOnly` set, yaw is
undefined — facing simply holds rather than producing `NaN`.

## Reading current speed

`PathFollow3D.CurrentSpeed` is written every step: it is `Speed` while the entity is actually
advancing, or zero while holding (a finished `Once` path, or a degenerate path that cannot move —
see below). Read it to pick a locomotion animation clip (idle vs. walk/run) without recomputing
displacement yourself — this is what the animation-selection work in #226 builds on.

## Degenerate paths

Fewer than two waypoints, or a run of duplicate/coincident waypoints that leaves nothing to travel,
are handled without throwing or producing `NaN` transforms: the entity's position and rotation are
simply left as-is, and `CurrentSpeed` reports zero. A bounded number of zero-length-leg advances per
step (rather than an unbounded loop) guards against a pathological all-coincident waypoint cycle
spinning forever.

## Out of scope

- **Pathfinding and obstacle avoidance** — waypoints are hand-authored; an entity will walk through
  a wall if routed through one. Physics is 2D-only and 3D ray queries (`World.Raycast`) are the only
  3D query support so far (see [queries.md](queries.md)).
- **Rider carrying** — unlike a `PlatformPath` platform, nothing else automatically rides along
  with a `PathFollow3D` entity the way `CharacterControllerSystem` carries a grounded controller on
  a moving 2D platform (see [physics.md](physics.md#moving-platforms-and-rider-carrying)).

## See also

- `src/Engine/Yaeger/Graphics/PathFollow3D.cs`
- `src/Engine/Yaeger/Systems/PathFollow3DSystem.cs`
- [physics.md](physics.md#platformpath-optional) — the 2D counterpart
- [tweening.md](tweening.md), [sequencing.md](sequencing.md) — for scripted, one-off beats instead
  of a looping patrol
