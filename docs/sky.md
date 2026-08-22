# Sky

Two independent ways to render what surrounds a 3D scene:

- **`Skybox`** — six images sampled as a cubemap. Static; see [pbr.md#image-based-lighting](pbr.md#image-based-lighting)
  for how it drives IBL via `EnvironmentMapRegistry`, prefiltered once at registration.
- **`ProceduralSky`** — a shader-computed sky with no assets: a gradient that tracks the sun, sun
  and moon discs, a rotating star field, and drifting clouds. This page covers that one, including
  its own throttled IBL path ([Sky-driven image-based lighting](#sky-driven-image-based-lighting)).

`MeshRenderSystem` dispatches whichever component the scene carries; a scene with neither renders no
sky at all, and a scene with both keeps drawing the cubemap (see [Dispatch](#dispatch) below) — so
adding `ProceduralSky` support never changes an existing `Skybox` scene's output.

## Setup

```csharp
using var proceduralSkyRenderer = new ProceduralSkyRenderer(window.Gl);

var skyEntity = world.CreateEntity("sky");
world.AddComponent(skyEntity, ProceduralSky.Default);

var skySystem = new ProceduralSkySystem(world);
window.OnUpdate += dt => skySystem.Update((float)dt);

var meshRenderSystem = new MeshRenderSystem(
    renderer3D,
    meshRegistry,
    textures,
    world,
    window,
    proceduralSkyRenderer: proceduralSkyRenderer
);
```

That draws a fixed noon sky. `ProceduralSkySystem.Update` only advances the cloud-scroll clock
(`ProceduralSky.Elapsed`) — the sun and moon stay wherever they were set, by hand or by the day/night
cycle below.

## Driving it from the day/night cycle

```csharp
var sun = world.CreateEntity("sun");
world.AddComponent(sun, TimeOfDay.Default with { DayLengthSeconds = 300f });

var dayNight = new DayNightCycleSystem(world);
window.OnUpdate += dt => dayNight.Update((float)dt);
```

With both a `TimeOfDay` and a `ProceduralSky` in the world, `DayNightCycleSystem` writes
`ProceduralSky.SunDirection`, `MoonDirection`, and `DaylightFactor` onto every `ProceduralSky` entity
each update — the same "auto-picked-up" relationship it has with `CelestialLight`-tagged light
entities (see [day-night.md](day-night.md#sun-and-moon)). No extra wiring: the sky tracks the sun on
its own once both components share a world.

A scene with no day/night cycle can still drive the sky by hand — set `SunDirection`/`MoonDirection`/
`DaylightFactor` directly and skip `DayNightCycleSystem` entirely.

## What gets rendered

| Feature | Driven by |
| --- | --- |
| Gradient (zenith → horizon → dusk warmth) | `SunDirection.Y` (elevation) and `DaylightFactor` |
| Sun disc + glow | `SunDirection` |
| Moon disc + glow | `MoonDirection`, `MoonPhase` |
| Star field | Rotated by `SunDirection`'s horizontal angle, faded by `1 - DaylightFactor`, density by `StarDensity` |
| Clouds | fBm noise scrolling by `CloudWind * Elapsed`, thresholded by `CloudCoverage`, scaled by `CloudScale`, lit from `SunDirection` |

```csharp
public record struct ProceduralSky
{
    public Vector3 SunDirection;    // written by DayNightCycleSystem if a TimeOfDay shares the world
    public Vector3 MoonDirection;
    public float DaylightFactor;    // [0, 1]; fades stars in/out, warms the dusk gradient band
    public Vector2 CloudWind;       // world-units/second
    public float CloudScale;        // higher = smaller, more numerous clouds
    public float CloudCoverage;     // [0, 1]; 0 is a clear sky
    public float StarDensity;       // [0, 1); higher is denser
    public float MoonPhase;         // [0, 1]; 0.5 is full, 0/1 is new
    public float Elapsed;           // advanced by ProceduralSkySystem; drives cloud scroll
}
```

`ProceduralSky.Default` is a clear noon sky: sun overhead, moon opposite and below the horizon, full
daylight, light cloud cover, and a dense star field that's simply invisible at `DaylightFactor = 1`.

### Moon phase is stylized, not astronomical

`MoonPhase` sweeps a shadow disc horizontally across the moon rather than computing a real lunar
terminator ellipse — symmetric and monotonic between new (0 or 1) and full (0.5), which is enough for
a game sky without the extra shader complexity a physically accurate phase would need. See the
comments above `moonPhaseMask` in `ProceduralSky.frag` for the exact construction.

### Why star rotation reuses the sun's angle

The star field is rotated by the sun direction's own horizontal-plane angle
(`ProceduralSkyMath.StarRotation`) rather than tracked by a separate sidereal clock — it isn't real
astronomy (a solar day and a star's apparent revolution aren't the same length), but a game doesn't
need that: it only needs a rotation that completes one turn per day/night cycle and holds still when
the sun does, which is exactly what a frozen `TimeOfDay` gives every other day/night-driven visual
already. This is also why the rotation is computed once per draw on the CPU and uploaded as a uniform
rather than recomputed per-fragment: it's what makes it directly unit-testable outside the shader
(`ProceduralSkyMathTests`), the same reasoning `BillboardMath` documents for camera axis extraction.

### Cloud projection has no seam

Clouds sample a planar dome projection of the view direction (`dir.xz / dir.y`), which is a
continuous function of `dir` rather than a wrapped UV atlas — there's no seam to hide because nothing
wraps. The projection does have a singularity as `dir.y` approaches the horizon, which is masked by
fading cloud contribution out over the same band (`horizonFade` in `ProceduralSky.frag`).

## Dispatch

```csharp
new MeshRenderSystem(
    renderer3D, meshRegistry, textures, world, window,
    skyboxRenderer: skyboxRenderer,           // six-image cubemap path
    cubemapRegistry: cubemapRegistry,
    proceduralSkyRenderer: proceduralSkyRenderer  // shader-computed path
)
```

Both parameters are optional and independent. `MeshRenderSystem` draws the cubemap `Skybox` when the
scene has one (unchanged from before this component existed); otherwise it draws `ProceduralSky` when
the scene has one and a renderer was supplied. A scene with neither renders no sky, same as omitting
both today.

## Sky-driven image-based lighting

A `ProceduralSky` can also light PBR materials — ambient and reflections that track the sky through a
full day/night cycle, rather than a flat driven `AmbientLight` — via `ProceduralSkyIbl`. It's the
dynamic counterpart to `EnvironmentMapRegistry` (which prefilters a static `Skybox` cubemap once, at
registration, and never again): `ProceduralSkyIbl` bakes the current sky into a cubemap and
re-prefilters it on a throttle, since the sky changes continuously but re-running the full prefilter
chain every frame would be wasteful.

```csharp
using var proceduralSkyRenderer = new ProceduralSkyRenderer(window.Gl);
using var iblPrefilter = new IblPrefilter(window.Gl);
var proceduralSkyIbl = new ProceduralSkyIbl(proceduralSkyRenderer, iblPrefilter);

window.OnUpdate += dt =>
{
    // ...ProceduralSkySystem.Update / DayNightCycleSystem.Update as usual...
    world.TryGetComponent<ProceduralSky>(skyEntity, out var sky);
    proceduralSkyIbl.Update(sky, (float)dt, (int)window.Size.X, (int)window.Size.Y);
};

var meshRenderSystem = new MeshRenderSystem(
    renderer3D, meshRegistry, textures, world, window,
    proceduralSkyRenderer: proceduralSkyRenderer,
    proceduralSkyIbl: proceduralSkyIbl
);
```

`Update` is caller-driven and belongs in `OnUpdate`, not `OnRender` — the same reason
`EnvironmentMapRegistry.Register` is a separate, explicit call rather than something
`MeshRenderSystem` triggers itself: tying the bake to the render loop would tie its cadence to frame
rate instead of to the throttle below. `MeshRenderSystem` only ever reads the result
(`ProceduralSkyIbl.TryGet`); when both a cubemap `Skybox`'s environment map and a `ProceduralSky`'s
are available, the cubemap one wins, matching the two sky renderers' own draw-order precedence.

### The throttle

`ProceduralSkyIbl.Settings` (`ProceduralSkyIblSettings`, defaults `MinRebakeInterval = 2s`,
`SunDirectionThresholdDegrees = 3°`) gates re-baking on **both** conditions at once: the minimum
interval must have elapsed, *and* the sun must have moved past the angle threshold since the last
bake. Requiring both — rather than either — is deliberate: the interval is a hard floor on re-bake
rate no matter how fast the sun moves (a short `DayLengthSeconds` would otherwise cross a small angle
threshold many times within one interval window, defeating the point of throttling at all), and the
angle check only skips a bake once the floor allows one but the sky genuinely hasn't changed enough to
be worth it (a paused or very slow cycle). The decision is a pure static
(`ProceduralSkyIbl.ShouldRebake`) so it's unit-tested directly, including a simulated fast day/night
cycle asserting the rebake count stays bounded.

The first `Update` call always bakes (there's nothing to compare against yet); after that, GL
resources are reused across bakes — `ProceduralSkyRenderer.Bake` renders into one persistent cubemap
it owns, and `ProceduralSkyIbl` disposes the previous `EnvironmentMap` before storing the new one — so
a cycle running for minutes doesn't leak texture handles.

### What doesn't change

A scene with a static six-image `Skybox` keeps using `EnvironmentMapRegistry`'s register-once path and
pays nothing extra by `ProceduralSkyIbl` existing — the two are independent, opt-in types. Nothing
about `Renderer3D.SetEnvironmentMap`/`DisableIBL` changed either: `ProceduralSkyIbl` produces the same
`EnvironmentMap` type `IblPrefilter.Prefilter` always has, just from a baked cubemap instead of a
loaded one.

## Serialization

`ProceduralSky` round-trips through prefab/scene JSON via `RegisterEngineComponents()`:

```json
{
  "tag": "sky",
  "components": [
    { "type": "ProceduralSky", "cloudCoverage": 0.3, "starDensity": 0.99 }
  ]
}
```

All properties are optional and default to `ProceduralSky.Default`. `sunDirection`/`moonDirection`/
`daylightFactor` are typically overwritten on the first `DayNightCycleSystem.Update` anyway (see
above) — the serialized values just give the scene a sane look before that first update, the same
role a serialized `DirectionalLight` plays in a day/night scene.

## Related

- [day-night.md](day-night.md) — the `TimeOfDay` clock that drives `SunDirection`/`MoonDirection`/`DaylightFactor`
- [pbr.md#image-based-lighting](pbr.md#image-based-lighting) — `IblPrefilter`/`EnvironmentMapRegistry`, and the cubemap `Skybox`'s register-once IBL path `ProceduralSkyIbl` mirrors
- [lighting.md](lighting.md) — the light components a moving sun also drives, and `AmbientLight` — the cheap fallback everywhere IBL is off
