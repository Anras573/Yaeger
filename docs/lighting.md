# 3D Lighting

`Renderer3D` accumulates contributions from three kinds of light each frame. All three work in
both the Blinn-Phong and PBR shading paths (see [pbr.md](pbr.md)).

| Light | Component | Position source | Falloff |
| --- | --- | --- | --- |
| Directional | `DirectionalLight` | n/a (infinitely far) | none |
| Point | `PointLight` | `Transform3D.Position` | range-based, smooth |
| Spot | `SpotLight` | `Transform3D.Position` | range-based + cone edge |

There is always exactly one directional light (the first `DirectionalLight` entity, or a sensible
default when none exists). Point and spot lights are optional and additive — a scene with none
renders exactly as it did before this feature existed.

The directional light can also cast shadows via shadow mapping — see [shadows.md](shadows.md).

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

- `Renderer3D.MaxPointLights` = **16**
- `Renderer3D.MaxSpotLights` = **8**

`MeshRenderSystem` collects up to these counts (extra light entities are ignored). If you call
`Renderer3D.SetPointLights` / `SetSpotLights` directly, lights past the cap are silently dropped.

## Falloff details

- **Distance attenuation** uses a smooth, range-windowed inverse-square curve: the contribution
  tapers off with distance and reaches exactly zero at `Range`, so there's no hard popping edge. A
  `Range` of zero disables the light.
- **Spot cone**: the beam is fully bright within `InnerConeAngle` and fades to dark by
  `OuterConeAngle` via `smoothstep`. Angles are clamped so `InnerConeAngle <= OuterConeAngle`.

`Direction` (spot) and all intensities/ranges are sanitised on upload — non-finite or negative
values are coerced to safe defaults, mirroring `SetSceneLighting`.

## Sample

`Samples/CornellBox` adds red, green, and blue `PointLight` entities inside the box to show
multiple coloured sources mixing across the walls and boxes.
