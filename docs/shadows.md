# Shadow Mapping

`Renderer3D` can cast shadows from the scene's directional light and, separately, from up to two
point lights. Both are produced with classic shadow mapping and are **opt-in** — a scene that never
creates a `ShadowMapRenderer`/`PointShadowMapRenderer` renders exactly as it did before either
feature existed. This page covers the directional path first (the older, cheaper one); see
[Point-light shadows](#point-light-shadows) below for the cube-map path.

> **Scope.** Directional: single cascade, no CSM. One map, so one caster: when a scene has two
> directional lights, the brighter one casts and the other is unshadowed. Point: capped at
> `Renderer3D.MaxShadowCastingPointLights` (2) casters at once — see below. Spot lights don't cast
> shadows yet.

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

Point lights are shadowed separately (see [Point-light shadows](#point-light-shadows)); spot lights
(see [lighting.md](lighting.md)) don't cast shadows yet.

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

## Point-light shadows

A `PointLight` with `CastsShadows` set casts a shadow in every direction via a **cube shadow map**:
six depth-only face captures from the light's position instead of the directional path's one
orthographic capture. This is what makes a brazier or a torch throw pillar shadows at night, when
point lights are often the only light in the scene at all.

### How it works

Each frame, for every selected shadow-casting point light, `MeshRenderSystem` runs the same
two-pass shape as the directional path — a depth pass, then the existing lighting pass — six times,
once per cube face:

1. **Shadow pass** (`PointShadowMapRenderer`). Casters within the light's `Range` (culled by
   distance — geometry outside a light's reach doesn't belong in its shadow pass regardless of
   camera visibility) are rendered from the light's position, once per face, into a cubemap. Rather
   than raw perspective depth, each face's fragment shader (`PointShadowMap.frag`) writes **linear
   distance from the light, normalized by its `Range`** — the standard point-shadow technique, and
   the reason cube shadows here have no seams or light leaks at face boundaries: distance is
   continuous across a cube's edges and corners, where non-linear perspective depth from two
   adjacent faces would disagree.
2. **Lighting pass** (existing, in `Renderer3D.frag`). For each point light, if it currently occupies
   one of the two shadow slots, the fragment's distance from that light is compared against the
   cubemap's stored value (sampled along the light-to-fragment direction) to decide shadowed/lit — no
   PCF; cube shadows are already the expensive part of this feature.

Skinned casters are skinned in the shadow pass too, the same way the directional path handles them.

### Usage

```csharp
using var pointShadowMapRenderer = new PointShadowMapRenderer(
    window.Gl,
    new PointShadowSettings
    {
        MapResolution = 512,  // per cube face
        NearPlane     = 0.05f,
        Bias          = 0.05f, // world-space distance, not normalized depth like ShadowSettings.Bias
    });

var meshRenderSystem = new MeshRenderSystem(
    renderer3D, registry, textures, world, window,
    pointShadowMapRenderer: pointShadowMapRenderer);

var brazier = world.CreateEntity("brazier");
world.AddComponent(brazier, new Transform3D(position, Quaternion.Identity, Vector3.One));
world.AddComponent(brazier, PointLight.Default with
{
    Range = 8f,
    CastsShadows = true,   // opt-in — false costs nothing
});
```

That's the whole wiring. Unlike directional shadows, there's no far plane to tune separately: the
light's own `Range` is the shadow far plane, since it already bounds how far the light reaches.

### The cap, and which lights win

Six face renders per light is expensive, so casting is capped at `Renderer3D.MaxShadowCastingPointLights`
(2), well below the 16-light shading budget. When more lights have `CastsShadows` set than the cap
allows, the ones **closest to the camera** win (`PointShadowMapRenderer.SelectShadowCasters`) — a
capped shadow budget is most worth spending on what the viewer can actually see up close. Lights past
the cap still light the scene exactly as `CastsShadows = false` would; they simply don't cast.

A world with no shadow-casting point lights (no `pointShadowMapRenderer` passed, or none flagged)
never enters the pass — no six-face renders, no `SetPointShadows` upload beyond the one "nothing to
show" call.

### `PointShadowSettings`

| Field | Meaning |
| --- | --- |
| `MapResolution` | Per-face cube shadow map dimension in texels. Six faces per light makes this expensive to raise — a few hundred is plenty for a light close to what it lights. |
| `NearPlane` | Near plane of each face's perspective capture. |
| `Bias` | World-space distance bias against shadow acne (not a normalized-depth bias like `ShadowSettings.Bias` — a point shadow's stored value is linear distance, not device depth). |

`PointShadowSettings.Default` is a 512² per-face map with a 0.05-unit near plane and bias.

### What's not (yet) here

Static-light caching — skipping the six-face re-render for a light whose casters haven't moved,
which a stationary brazier's shadow would benefit from every frame — is a natural follow-up once
this is in use; it isn't implemented yet, so every selected shadow-casting point light re-renders
its six faces every frame. Spot lights don't cast shadows at all yet (a single perspective map would
be the cheaper cousin of this feature, much closer to the directional path).

## Sample

`Samples/CornellBox` enables a 2048² PCF shadow map. The two interior boxes cast visible shadows
across the floor from the angled directional light.

For a light that moves, see [day-night.md](day-night.md) — a `TimeOfDay`-driven sun works with the
shadow rig as-is, and `AutoFit` is what keeps its shadows framed from sunrise to sunset.
