# Directional Shadow Mapping

`Renderer3D` can cast hard or PCF-soft shadows from the scene's directional light. Shadows are
produced with classic two-pass shadow mapping and are **opt-in** — a scene that never creates a
`ShadowMapRenderer` renders exactly as it did before this feature existed.

> **Scope.** Directional light only (point/spot shadows are a follow-up). Single cascade, no CSM.
> One map, so one caster: when a scene has two directional lights, the brighter one casts and the
> other is unshadowed.

## How it works

Each frame `MeshRenderSystem` runs two passes:

1. **Shadow pass.** The scene is rendered from the directional light's point of view into an
   off-screen depth texture (the *shadow map*) using a depth-only shader and an orthographic
   projection. All meshes are drawn regardless of the camera frustum, so off-screen geometry can
   still cast into view. Skinned meshes are skinned in this pass too (the same bone palette as the
   main pass, applied to position only), so an animated character's shadow follows its current pose
   frame by frame rather than casting a static bind-pose silhouette.
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
| `AutoFit` | Fit the frustum to the casters' bounding sphere each frame instead of using `OrthographicSize`. Off by default. |
| `HorizonFadeElevation` | Light elevation below which shadows fade out, reaching zero at the horizon. |

`ShadowSettings.Default` is a 2048² map with PCF enabled, auto-fit off, and a narrow horizon fade.

## Moving lights

A hand-tuned `OrthographicSize` only holds for the angle it was tuned at. A light near the horizon
casts shadows far longer than the extent that frames it overhead, so casters fall outside the
frustum and their shadows simply vanish. Three things make a moving light — a day/night sun, say —
behave:

- **`AutoFit`** reframes the light on the casters' bounding sphere every frame. The fit is
  independent of the light's angle, so one setting holds across a full arc. It costs a pass over
  the `Aabb3D` store, and a scene much wider than the map's texel density gets blockier shadows
  than a tight hand-tuned extent would give — which is why it is opt-in. Skinned meshes carry no
  `Aabb3D` by design, so they don't contribute to the fit.
- **The up vector rotates smoothly** as the light approaches vertical. Building the light's
  look-at needs an up vector that isn't parallel to it, and switching between two fixed axes at a
  threshold rotates the shadow map ~90° in the single frame the light crosses it — every shadow in
  the scene snaps at once. Blending across a band spreads that turn over the transit, so shadows
  swim slightly instead of popping. (A `TimeOfDay`'s `AxisTilt` avoids the region altogether; this
  keeps it well-behaved for lights that do pass through it.)
- **Shadows fade out at the horizon.** A light at or below the horizon casts none at all: it lights
  nothing a shadow would fall on, and its map degenerates as the frustum flattens along the horizon.
  `HorizonFadeElevation` spreads that transition over a few degrees so a setting sun dims its
  shadows out instead of dropping them in one frame. Below the horizon the whole shadow pass is
  skipped, so it costs nothing.

Shadow strength is uploaded to the lighting pass, so the fade is a real dimming of the shadow rather
than an on/off switch — see `Renderer3D.SetShadowMap`'s `strength` parameter.

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

For a light that moves, see [day-night.md](day-night.md) — a `TimeOfDay`-driven sun works with the
shadow rig as-is, and `AutoFit` is what keeps its shadows framed from sunrise to sunset.
