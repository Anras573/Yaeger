# Day/Night Cycle

A data-driven sun: one `TimeOfDay` component drives the scene's directional light, its ambient
term, and a suggested tone-map exposure, from sunrise through noon to a moonlit night.

```csharp
var sun = world.CreateEntity("sun");
world.AddComponent(sun, TimeOfDay.Default with { DayLengthSeconds = 120f });

var dayNight = new DayNightCycleSystem(world);

window.OnUpdate += dt => dayNight.Update((float)dt);
```

That's the whole setup. The system writes a `DirectionalLight` and an `AmbientLight` onto the same
entity every update, and `MeshRenderSystem` re-reads both every frame — so the shading and the
shadow map follow the sun with no further wiring. A scene that wants the sun and the moon lit at
once tags two entities instead; see [Sun and moon](#sun-and-moon).

## The clock

```csharp
public record struct TimeOfDay
{
    public float NormalizedTime;    // [0, 1): 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset
    public float DayLengthSeconds;  // real seconds per cycle; <= 0 freezes the clock
    public float NorthOffset;       // radians; rotates the arc about Y — where the sun rises from
    public float AxisTilt;          // radians; leans the arc so it misses the zenith
}
```

`TimeOfDay.Midnight`, `.Sunrise`, `.Noon`, and `.Sunset` are constants for the four positions.

`NormalizedTime` is the single source of truth, and the evaluation is a pure function of it — the
system holds no accumulated state of its own. Assigning it scrubs the cycle to any hour, and the
result is identical whether you arrived there by scrubbing or by simulating forty frames:

```csharp
// Jump to dusk and apply it immediately.
world.AddComponent(sun, time with { NormalizedTime = TimeOfDay.Sunset });
dayNight.Update(0f);
```

Values outside `[0, 1)` are wrapped, so nothing special is needed at the end of a day. A
`DayLengthSeconds` of zero or less freezes the clock but still applies its lighting each update —
a paused cycle holds its look rather than going dark, and can still be scrubbed by hand.

### Why `AxisTilt` defaults to non-zero

At zero tilt the sun passes exactly through the zenith at noon — the degenerate case for a look-at
matrix, and the region where `ShadowMapRenderer` has to rotate the up vector it builds the
light-space matrix from. That rotation is smooth (see [shadows.md](shadows.md#moving-lights)), so
the zenith is no longer a hazard, but a shadow map turning through 90° still makes shadows swim.
The default ~20° tilt keeps the arc beside the zenith and avoids the region altogether.

Tilting rotates about X, which scales only the vertical component, so the horizon crossings stay
exactly at elevation zero however far the arc leans.

## What gets evaluated

`DayNightCycle.Evaluate(time, settings)` is public and pure — no world, no GL — and returns:

| Field | What it is |
| --- | --- |
| `Sun` / `Moon` | Each body's `DirectionalLight`, dark while that body is below the horizon |
| `KeyLight` | Whichever of the two is above the horizon, for a scene driving a single light |
| `Ambient` | The `AmbientLight` for this moment — day, twilight, or night |
| `Exposure` | Suggested tone-map exposure (see below) |
| `SunDirection` / `MoonDirection` | Unit vectors towards each body |
| `DaylightFactor` | `[0, 1]` night→day blend — useful for fading anything else with the light |

`SunElevation` (the sine of the sun's altitude) and `IsDaytime` are derived from `SunDirection`.

### Sun and moon

`Sun` and `Moon` are always both evaluated. Each ramps its intensity up from zero at its own
horizon, so a body that is down is fully dark rather than merely dim — which means both can sit in
the scene permanently instead of being switched on and off.

By default the cycle drives **one** light: `KeyLight`, whichever body is up, written onto the clock
entity. That is enough for most scenes and costs one directional slot.

To light dawn and dusk with both at once — the moon already risen while the sun is still setting —
tag two entities with `CelestialLight` and the cycle writes each body to its own:

```csharp
var sun = world.CreateEntity("sun");
world.AddComponent(sun, new TimeOfDay { DayLengthSeconds = 120f, AxisTilt = 0.35f });
world.AddComponent(sun, new CelestialLight(CelestialBody.Sun));

var moon = world.CreateEntity("moon");
world.AddComponent(moon, new CelestialLight(CelestialBody.Moon));
```

The clock and the sun can share an entity, as above, or not — `TimeOfDay` and `CelestialLight` are
independent. Once any `CelestialLight` exists the cycle stops writing `KeyLight` to the clock
entity, so it doesn't quietly occupy a third directional slot.

`Renderer3D.MaxDirectionalLights` is 2, so a scene using both bodies has no directional slots left
for anything else. See [lighting.md](lighting.md).

### Exposure is reported, not applied

`ToneMapEffect` lives in the native runtime and `DayNightCycleSystem` lives in `Yaeger.Core`, so
the system can't reach it. Read the value and assign it yourself:

```csharp
toneMap.Exposure = dayNight.CurrentLighting.Exposure;
```

Ignoring it is fine — the scene is simply darker at night. It matters most on an HDR post chain,
where day and night are several stops apart. See [post-processing.md](post-processing.md).

## Ambient

`AmbientLight` is the flat term the PBR path adds to every fragment when image-based lighting is
off. It exists as its own component because a day/night cycle is mostly an *ambient* story indoors:
in an enclosed scene, direct sun reaches very little, so without a driven ambient noon and midnight
look nearly identical under a roof.

```csharp
public record struct AmbientLight
{
    public Color Color;
    public float Intensity;
}
```

`MeshRenderSystem` picks up the first `AmbientLight` in the world each frame, the same "first one
wins" convention `DirectionalLight` and `Camera3D` use. `AmbientLight.Default` is white at `0.03`,
exactly the constant the shader used to hardcode, so a scene that never attaches one is unchanged.

Two things it doesn't affect:

- **Scenes with image-based lighting.** A prefiltered `EnvironmentMap` supplies a directional
  ambient that's strictly better than one flat colour, and wins while it's bound. See
  [pbr.md#image-based-lighting](pbr.md#image-based-lighting).
- **The Blinn-Phong path**, whose ambient is per-material (`Material3D.Ambient`) and predates this
  component. Folding a scene-wide term into it would change every existing Blinn-Phong scene. Drive
  `Material3D.Ambient` there, or opt into `Material3D.UsePbr`.

## Art direction

`DayNightCycleSettings` holds the tunables — key-light colours and intensities, the three ambient
stops, exposure, and where along the arc day becomes night:

```csharp
var settings = DayNightCycleSettings.Default with
{
    SunIntensity = 5f,
    NightAmbient = new AmbientLight { Color = new Color(40, 60, 110), Intensity = 0.02f },
};

var dayNight = new DayNightCycleSystem(world, settings);
dayNight.Settings = settings with { SunIntensity = 2f };  // mutable at runtime
```

Like `ShadowSettings`, `default(DayNightCycleSettings)` is all-zero (a black scene) rather than
sensible — start from `Default` and override with `with`.

The ambient runs across three stops rather than a night→day lerp: `NightAmbient`, `TwilightAmbient`
at the horizon crossings, and `DayAmbient`. Twilight gets its own stop because it's the moment the
key light is dimmest, so nearly everything visible is ambient — a straight lerp passes through dusk
without it ever looking like dusk.

`DaylightElevation` and `NightElevation` are sun elevations (sines of altitude), not times: the band
between them is the twilight the ambient blends across, and `DaylightElevation` also sets how far
the sun climbs before reaching full intensity and colour.

This is deliberately a handful of stops rather than a keyframe track, in the same spirit as
`AnimationStateMachine` — enough to art-direct a cycle, not a curve editor.

## Shadows

The sun works with the shadow rig as-is: `MeshRenderSystem` recomputes the light-space matrix from
the casting light every frame. Two settings matter for a light that moves, both covered in
[shadows.md](shadows.md#moving-lights):

```csharp
using var shadowMap = new ShadowMapRenderer(gl, ShadowSettings.Default with
{
    AutoFit = true,               // frame the casters, not a hand-tuned extent
    HorizonFadeElevation = 0.1f,  // dim shadows out as the sun sets
});
```

Without `AutoFit`, a fixed `OrthographicSize` frames the scene at one sun angle and loses the long
shadows at every other. With it, the frustum is fitted to the casters' bounding sphere each frame,
which is independent of where the light is.

With two bodies lit, the brighter one casts — the sun by day, the moon by night, switching while
both are dim. And because a body below the horizon has zero intensity, the shadow pass is skipped
outright for it.

## Ordering

Run `DayNightCycleSystem.Update` with the other gameplay updates, before rendering. It only writes
components, so it has no ordering constraints against physics or animation.

One constraint worth knowing: the scene's directional lights should be the cycle's.
`MeshRenderSystem` accumulates the first `Renderer3D.MaxDirectionalLights` it finds, so an unrelated
light entity can take a slot the cycle expected and leave a body unlit.

## Scenes

Both components round-trip through prefab and scene JSON via `RegisterEngineComponents()`:

```json
{
  "tag": "sun",
  "components": [
    { "type": "TimeOfDay", "normalizedTime": 0.3, "dayLengthSeconds": 240.0, "axisTilt": 0.35 },
    { "type": "CelestialLight", "body": "Sun" },
    { "type": "AmbientLight", "color": [180, 205, 255], "intensity": 0.3 }
  ]
}
```

`DayNightCycleSettings` is not serialized — it's art direction shared by a whole game rather than
per-entity scene data, so it's supplied in code.

## Related

- [lighting.md](lighting.md) — the light components the cycle drives
- [shadows.md](shadows.md) — the shadow rig the moving sun feeds
- [pbr.md](pbr.md) — where the ambient term lands in the shading model
