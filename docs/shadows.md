# Directional Shadow Mapping

`Renderer3D` can cast hard or PCF-soft shadows from the scene's directional light. Shadows are
produced with classic two-pass shadow mapping and are **opt-in** — a scene that never creates a
`ShadowMapRenderer` renders exactly as it did before this feature existed.

> **Scope (v1).** Directional light only (point/spot shadows are a follow-up). Single cascade, no
> CSM. The shadow map has a fixed resolution and does not auto-fit the camera frustum.

## How it works

Each frame `MeshRenderSystem` runs two passes:

1. **Shadow pass.** The scene is rendered from the directional light's point of view into an
   off-screen depth texture (the *shadow map*) using a depth-only shader and an orthographic
   projection. All meshes are drawn regardless of the camera frustum, so off-screen geometry can
   still cast into view.
2. **Lighting pass (existing).** Each fragment is projected into light space and its depth compared
   against the shadow map. Occluded fragments lose the directional light's contribution. An optional
   3×3 PCF kernel softens the edges.

Only the directional light is shadowed; point and spot lights (see [lighting.md](lighting.md)) are
unaffected.

## Usage

Create a `ShadowMapRenderer` and pass it to `MeshRenderSystem`:

```csharp
using var renderer3D = new Renderer3D(window.Gl);

using var shadowMapRenderer = new ShadowMapRenderer(
    window.Gl,
    new ShadowSettings
    {
        MapResolution    = 2048,   // square depth texture dimension
        OrthographicSize = 2.5f,   // half-width of the light's frustum, world units
        NearPlane        = 0.1f,
        FarPlane         = 12f,
        Bias             = 0.004f, // depth bias against shadow acne
        EnablePcf        = true,   // 3x3 soft-shadow filter
    });

var meshRenderSystem = new MeshRenderSystem(
    renderer3D, registry, textures, world, window,
    shadowMapRenderer: shadowMapRenderer);
```

That's the whole wiring — `MeshRenderSystem` orchestrates both passes and uploads the shadow map to
the renderer automatically. The light's orthographic frustum is centred on the active camera's
`Target`, so size `OrthographicSize`/`FarPlane` to enclose the part of the scene that should receive
shadows.

## `ShadowSettings`

| Field | Meaning |
| --- | --- |
| `MapResolution` | Shadow map dimension in texels. Higher = crisper shadows, more memory/fill. |
| `OrthographicSize` | Half-extent of the light's orthographic frustum in world units. |
| `NearPlane` / `FarPlane` | Depth range of the light's projection. The scene must fit between them. |
| `Bias` | Depth offset subtracted during the shadow test. Too low → *acne*; too high → *peter-panning*. |
| `EnablePcf` | When true, averages a 3×3 kernel for soft edges; otherwise hard shadows. |

`ShadowSettings.Default` is a 2048² map with PCF enabled.

## Tuning notes

- **Shadow acne** (self-shadowing moiré): raise `Bias`. The shader also applies a slope-scaled term,
  so grazing angles already get extra offset.
- **Peter-panning** (shadows detached from their caster): lower `Bias`, or shrink `OrthographicSize`
  so each texel covers less world space.
- **Blocky edges**: raise `MapResolution` or tighten `OrthographicSize`; enable PCF for softer edges.
- Fragments outside the light's frustum sample a white (depth 1.0) border and are treated as fully
  lit, so there is no hard cutoff at the frustum boundary.

## Sample

`Samples/CornellBox` enables a 2048² PCF shadow map. The two interior boxes cast visible shadows
across the floor from the angled directional light.
