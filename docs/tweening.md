# Tweening

`TweenSystem` animates individual component fields — a transform's position, a light's intensity,
a material's emissive colour — from one value to another over time, eased by a named curve. It is
the engine's one general-purpose motion primitive: instead of hand-writing accumulator math in
`OnUpdate` for every lift, door, dimming light, or fading material, attach a `Tween` component and
let the system drive it.

## Quick start

```csharp
var tweenSystem = new TweenSystem(world);

var door = world.CreateEntity("door");
world.AddComponent(door, new Transform2D(new Vector2(0f, 0f)));
world.AddComponent(
    door,
    Tween.Create(
        door,
        TweenChannel.Transform2DPosition,
        new Vector2(0f, 0f),
        new Vector2(0f, 2f),
        duration: 1.5f,
        easing: EasingFunction.CubicOut
    )
);

window.OnUpdate += dt => tweenSystem.Update((float)dt);
```

`TweenSystem` implements `IUpdateSystem` and lives in `Yaeger.Core` (no `Window`/GL dependency, same
as `TransformHierarchySystem`), so it works in headless tests as well as native games. See
`Samples/TweenDemo` for a complete program animating a transform, a light, and a material together.

## The `Tween` component

| Field | Type | Meaning |
|-------|------|---------|
| `Target` | `Entity` | The entity whose component is written to |
| `Channel` | `TweenChannel` | Which field on `Target` is animated |
| `From` / `To` | `Vector4` | Start/end value, packed per channel (see below) |
| `Duration` | `float` | Seconds from the end of `Delay` to reaching `To`; must be positive |
| `Delay` | `float` | Seconds to hold at `From` before advancing; non-negative |
| `Easing` | `EasingFunction` | Curve applied to normalized progress before interpolating |
| `LoopMode` | `TweenLoopMode` | `Once`, `Loop`, or `PingPong` |
| `ElapsedTime` | `float` | Total time alive, including `Delay`; advanced by `TweenSystem` |
| `IsFinished` | `bool` | Set once a `Once`-mode tween reaches `To`; mirrors `AnimationState.IsFinished` |

Use the `Tween.Create(...)` overloads (taking `float`, `Vector2`, `Vector3`, `Quaternion`, or
`Color`) to build a `Tween` from values in their natural type — they pack into `From`/`To`
automatically. Reach for the primary constructor only when you already have a `Vector4`.

### One `Tween` per entity — the `Target` field and carrier entities

`ComponentStorage<T>` holds a single component of each type per entity, so an entity carrying its
own `Tween` can only animate one channel at a time. `Target` decouples "the entity the `Tween`
lives on" from "the entity being animated": the common case is **self-tweening**, where `Target`
is the same entity the component is attached to (as in the door example above).

To animate two channels on one entity concurrently — a door sliding open while its own panel light
brightens, say — give the second channel its own **carrier entity**: a lightweight entity whose
only component is a `Tween` with `Target` pointing at the entity actually being animated.

```csharp
// The cube itself owns the position tween...
world.AddComponent(cube, Tween.Create(cube, TweenChannel.Transform3DPosition, from, to, 2f));

// ...a separate, otherwise-empty carrier entity drives its emissive colour concurrently.
var emissiveCarrier = world.CreateEntity();
world.AddComponent(
    emissiveCarrier,
    Tween.Create(cube, TweenChannel.Material3DEmissiveColor, Color.Black, glow, 1.6f)
);
```

There is no pooling and no fixed track count — as many carrier entities as needed can target the
same entity, each animating a different channel independently.

## Channels

`TweenChannel` is a closed enum, not a reflection-based property path. Each channel's `From`/`To`
pack into the shared `Vector4` as follows:

| Channel group | Fields | `Vector4` packing |
|---|---|---|
| `Transform2DPosition` / `Scale`, `LocalTransform2DPosition` / `Scale` | `Vector2` | `X`, `Y` |
| `Transform2DRotation`, `LocalTransform2DRotation` | `float` (radians) | `X` |
| `Transform3DPosition` / `Scale`, `LocalTransform3DPosition` / `Scale`, `Camera3DPosition`, `Camera3DTarget` | `Vector3` | `X`, `Y`, `Z` |
| `Transform3DRotation`, `LocalTransform3DRotation` | `Quaternion` | `X`, `Y`, `Z`, `W` — always **slerped**, never lerped |
| `PointLightIntensity`, `SpotLightIntensity`, `DirectionalLightIntensity`, `Material3DOpacity` | `float` | `X` |
| `PointLightColor`, `SpotLightColor`, `DirectionalLightColor`, `Material3DEmissiveColor` | `Color` | Normalized RGBA via `Color.ToVector4()` |

`LocalTransform2D`/`LocalTransform3D` channels animate a hierarchy child in its parent's local
space — pair with `Parent` and run `TweenSystem` before `TransformHierarchySystem` in the same
frame so the composed world transform reflects the tweened value that frame.

## Loop modes

- **`Once`** — plays `From` → `To` once, then holds at `To` and sets `IsFinished`. Subsequent
  `Update` calls skip the tween entirely (a cheap no-op), matching `AnimationSystem`'s
  finished-and-non-looping short-circuit.
- **`Loop`** — wraps back to `From` and repeats indefinitely; `IsFinished` stays `false` forever.
- **`PingPong`** — alternates `From` → `To` → `From` indefinitely; `IsFinished` stays `false`
  forever.

Progress is computed directly from `ElapsedTime` each frame (`elapsed % (duration * 2)` for
ping-pong), not by tracking a direction flag, so looping and ping-ponging are frame-rate
independent and correct across a wrap — a very large `deltaTime` in one frame still lands on the
right point in the cycle.

## Easing

`Easing` is a static class of pure, allocation-free curve functions, each mapping normalized
progress `t` in `[0, 1]` to an eased factor: `Linear`, `QuadIn`/`Out`/`InOut`,
`CubicIn`/`Out`/`InOut`, `QuartIn`/`Out`/`InOut`, `SineIn`/`Out`/`InOut`, `ExpoIn`/`Out`/`InOut`,
`BackIn`/`Out`/`InOut`, `ElasticIn`/`Out`/`InOut` — standard formulas from easings.net.
`Easing.Apply(EasingFunction, float)` dispatches by enum; the individual static methods can be
called directly too (e.g. for a hand-rolled camera shake that doesn't go through a `Tween`).

`BackIn`/`BackOut`/`BackInOut` and the elastic variants intentionally overshoot past `0`/`1` before
settling — expected, not a bug.

## Prefab/scene authoring

`Tween` is registered in `RegisterEngineComponents()` (type id `"Tween"`), so it can be authored in
prefab/scene JSON and round-trips through `SceneSaver`/`SceneLoader`:

```json
{
  "type": "Tween",
  "targetTag": "doorLight",
  "channel": "PointLightIntensity",
  "from": [0.2, 0.0, 0.0, 0.0],
  "to": [1.5, 0.0, 0.0, 0.0],
  "duration": 2.0,
  "delay": 0.0,
  "easing": "CubicInOut",
  "loopMode": "PingPong"
}
```

`channel` and `duration` are required; `from`/`to` default to the zero vector, `delay` to `0`,
`easing` to `Linear`, and `loopMode` to `Once`. `targetTag` (optional) is the tag of the entity this
tween animates, resolved the same way `Parent`'s `parentTag` is — when omitted, the tween targets
the entity it is attached to (self-tweening).

## Known limitations

- No sequencing or chaining (playing one tween after another finishes) built into `Tween`/
  `TweenSystem` itself — see [sequencing.md](sequencing.md) for `SequenceSystem`, which drives
  ordered/parallel steps (including starting tweens and waiting on them) on top of this.
- No curve/spline editors or a graphical timeline.
- Channels are a closed enum, not reflection-based property paths — animating an arbitrary
  component field requires adding a new `TweenChannel` case to `TweenSystem`.
- `AnimationSystem` already owns 2D sprite-frame animation; `Tween` does not animate `Sprite`/
  `SpriteSheet` frame indices.
