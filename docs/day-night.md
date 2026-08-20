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
shadow map follow the sun with no further wiring.

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

At zero tilt the sun passes exactly through the zenith at noon. That's the degenerate case for a
look-at matrix, and it's where `ShadowMapRenderer` swaps the up vector it builds the light-space
matrix from — so shadows visibly snap as the sun crosses over. The default ~20° tilt keeps the arc
beside the zenith. Tilting rotates about X, which scales only the vertical component, so the
horizon crossings stay exactly at elevation zero however far the arc leans.

## What gets evaluated

`DayNightCycle.Evaluate(time, settings)` is public and pure — no world, no GL — and returns:

| Field | What it is |
| --- | --- |
| `KeyLight` | The `DirectionalLight` for this moment: the sun above the horizon, the moon below |
| `Ambient` | The `AmbientLight` for this moment — day, twilight, or night |
| `Exposure` | Suggested tone-map exposure (see below) |
| `SunDirection` / `MoonDirection` | Unit vectors towards each body, whichever is the key light |
| `DaylightFactor` | `[0, 1]` night→day blend — useful for fading anything else with the light |

`SunElevation` (the sine of the sun's altitude) and `IsDaytime` are derived from `SunDirection`.

### One key light, sun and moon

`Renderer3D` has a single directional slot, so the cycle picks whichever body is above the horizon
rather than lighting with both. Both ramp their intensity up from zero at their own horizon, so the
direction flip happens while the key light contributes nothing — the light never swings across the
sky. A true simultaneous sun and moon at dusk needs a second directional slot in the renderer.

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

## Ordering

Run `DayNightCycleSystem.Update` with the other gameplay updates, before rendering. It only writes
components, so it has no ordering constraints against physics or animation.

One constraint worth knowing: the cycle's entity should be the scene's **only** `DirectionalLight`.
`MeshRenderSystem` takes the first one it finds, so a light entity created earlier would win and
the cycle would appear to do nothing.

## Scenes

Both components round-trip through prefab and scene JSON via `RegisterEngineComponents()`:

```json
{
  "tag": "sun",
  "components": [
    { "type": "TimeOfDay", "normalizedTime": 0.3, "dayLengthSeconds": 240.0, "axisTilt": 0.35 },
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
