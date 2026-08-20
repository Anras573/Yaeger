# 3D Lighting

`Renderer3D` accumulates contributions from three kinds of light each frame. All three work in
both the Blinn-Phong and PBR shading paths (see [pbr.md](pbr.md)).

| Light | Component | Position source | Falloff |
| --- | --- | --- | --- |
| Directional | `DirectionalLight` | n/a (infinitely far) | none |
| Point | `PointLight` | `Transform3D.Position` | range-based, smooth |
| Spot | `SpotLight` | `Transform3D.Position` | range-based + cone edge |

Up to two directional lights are accumulated (the first two `DirectionalLight` entities, or a
sensible default when none exists). Point and spot lights are optional and additive — a scene with
none renders exactly as it did before this feature existed.

A directional light can also cast shadows via shadow mapping — see [shadows.md](shadows.md). There
is one shadow map, so one caster: with two directional lights the brighter one casts and the other
is unshadowed.
Both point/spot lights and shadows light a `Transparent`-blend-mode material the same as an
opaque one; the one difference is that a `Transparent` material does not itself cast a shadow
(see [pbr.md#transparency](pbr.md#transparency)).

## Components

```csharp
public record struct PointLight
{
    public Color Color;
    public float Intensity;
    public float Range;   // distance at which the contribution reaches zero
}

public record struct SpotLight
{
    public Color   Color;
    public float   Intensity;
    public Vector3 Direction;       // beam axis, from the light outward
    public float   InnerConeAngle;  // radians; fully lit at or below this half-angle
    public float   OuterConeAngle;  // radians; fully dark beyond this half-angle
    public float   Range;
}
```

Attach a `PointLight` or `SpotLight` alongside a `Transform3D`; the transform's `Position` places
the light in the world. `MeshRenderSystem` queries these entities every frame and uploads them to
the renderer automatically — no manual wiring beyond creating the entity.

```csharp
var lamp = world.CreateEntity("lamp");
world.AddComponent(lamp, new Transform3D(new Vector3(0, 3, 0), Quaternion.Identity, Vector3.One));
world.AddComponent(lamp, new PointLight { Color = Color.White, Intensity = 5f, Range = 8f });
```

## Limits

The fragment shader uploads fixed-size uniform arrays, so there is a hard cap per frame:

- `Renderer3D.MaxDirectionalLights` = **2**
- `Renderer3D.MaxPointLights` = **16**
- `Renderer3D.MaxSpotLights` = **8**

`MeshRenderSystem` collects up to these counts (extra light entities are ignored). If you call
`Renderer3D.SetPointLights` / `SetSpotLights` directly, lights past the cap are silently dropped.

## Scene ambient

Alongside the three light types there is a flat, scene-wide ambient term — the stand-in for light
arriving from everywhere rather than from a source. Attach an `AmbientLight` to any entity and
`MeshRenderSystem` uploads the first one it finds each frame:

```csharp
public record struct AmbientLight
{
    public Color Color;
    public float Intensity;
}
```

It applies to the PBR path only, and only while image-based lighting is off; `AmbientLight.Default`
(white at `0.03`) reproduces the constant the shader used before the component existed. See
[day-night.md#ambient](day-night.md#ambient) for the details and for the cycle that drives it.

## Falloff details

- **Distance attenuation** uses a smooth, range-windowed inverse-square curve: the contribution
  tapers off with distance and reaches exactly zero at `Range`, so there's no hard popping edge. A
  `Range` of zero disables the light.
- **Spot cone**: the beam is fully bright within `InnerConeAngle` and fades to dark by
  `OuterConeAngle` via `smoothstep`. Angles are clamped so `InnerConeAngle <= OuterConeAngle`.

`Direction` (spot) and all intensities/ranges are sanitised on upload — non-finite or negative
values are coerced to safe defaults, mirroring `SetSceneLighting`.

## Flickering lights

A `Tween` ping-ponging `PointLightIntensity` gives a smooth, perfectly periodic pulse — a breathing
lamp, not fire, and every light tweened that way pulses in lockstep. `LightFlicker` +
`LightFlickerSystem` instead drive irregular, continuous noise:

```csharp
public record struct LightFlicker
{
    public float BaseIntensity;   // intensity flickered around, and restored to on removal
    public float Amplitude;       // BaseIntensity +/- this, via noise in [-1, 1]
    public float Frequency;       // how fast the flicker evolves
    public float Seed;            // decorrelates two flickers with identical other settings
    public float PositionJitter;  // radius of a small positional wobble; 0 disables it
    public float Elapsed;         // advanced by the system; not meant to be set by hand
}
```

Attach it alongside a `PointLight` and/or `SpotLight` (both are written if both are present); run
`LightFlickerSystem.Update` each frame before any render system reads lights/transforms, the same
ordering convention as `CameraFollowSystem`:

```csharp
var brazier = world.CreateEntity();
world.AddComponent(brazier, new Transform3D(new Vector3(2f, 1f, 0f), Quaternion.Identity, Vector3.One));
world.AddComponent(brazier, new PointLight { Color = new Color(255, 140, 40), Range = 6f });
world.AddComponent(brazier, new LightFlicker
{
    BaseIntensity = 3f,
    Amplitude = 1.2f,
    Frequency = 4f,
    Seed = 1f,       // a second brazier should use a different seed
    PositionJitter = 0.05f,
});

var lightFlickerSystem = new LightFlickerSystem(world);
window.OnUpdate += dt => lightFlickerSystem.Update((float)dt);
```

The signal (`LightFlickerSignal`, pure static, unit-tested for range/continuity/framerate
independence) sums two octaves of `ValueNoise3D` — the same continuous noise primitive
`ParticlePool3D`'s turbulence term uses — rather than a sine, and is a pure function of `Elapsed`, so
the same wall-clock interval samples the same intensity regardless of frame rate. `Seed` offsets the
sampled point, so two `LightFlicker`s with identical other settings but different seeds flicker
independently.

Position jitter is applied and undone every update rather than accumulated: `LightFlickerSystem`
tracks the offset it last added per entity and subtracts it before computing a new one, so it never
drifts and never permanently overwrites a position some other system also writes (a torch parented
to a moving character, say). Removing `LightFlicker` (or destroying the entity) restores the light
to `BaseIntensity` and undoes the last position offset, instead of leaving either wherever the last
sampled frame happened to land.

## Samples

`Samples/CornellBox` adds red, green, and blue `PointLight` entities inside the box to show
multiple coloured sources mixing across the walls and boxes.

## Two directional lights

Two slots exist for one reason: a day/night cycle needs a sun and a moon, and around dawn and dusk
both are above the horizon at once. Swapping a single light's direction at the crossing is the
alternative, and it is exactly the discontinuity two slots avoid.

Nothing else changes. A scene with one directional light behaves as it always has, and a scene with
none still gets the default light. Lights past the second are ignored, the same way point and spot
lights past their caps are.

A directional light doesn't have to be static: `TimeOfDay` + `DayNightCycleSystem` drive direction,
colour, and intensity — plus the scene ambient — from a single clock, and can drive both bodies at
once. See [day-night.md](day-night.md).

`Samples/DamagedHelmet` combines a directional sun with warm-fill and cool-rim `PointLight`
entities around a glTF model, and is the reference for the `Skybox` component: it registers a
procedurally generated cubemap in a `CubemapRegistry` and passes a `SkyboxRenderer` to
`MeshRenderSystem`.
