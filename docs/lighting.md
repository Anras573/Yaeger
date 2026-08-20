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
