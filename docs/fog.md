# Distance Fog

`Renderer3D` can mix a depth-dependent tint into every shaded fragment as an inexpensive
atmospheric cue — a handful of shader instructions, no extra passes, no new render targets. Fog is
**opt-in** — a scene that never attaches a `FogSettings` renders exactly as it did before this
feature existed, including a scene sharing a `Renderer3D` with one that does enable fog.

## How it works

`MeshRenderSystem` looks for the first `FogSettings` entity in the world each frame (the same
"first X in the world" convention `AmbientLight` and `DirectionalLight` use) and uploads it via
`Renderer3D.SetFog`; a world with none calls `Renderer3D.DisableFog` instead.

Fog is applied identically in the PBR and Blinn-Phong shading branches, after lighting and
emissive, before the alpha write — so opaque, transparent, and additive surfaces at the same depth
receive the same fog. The skybox is left unfogged for this first cut (see the discussion on
[issue #217](https://github.com/Anras573/Yaeger/issues/217)) — fogging it is usually right for a
ground-level scene, but an open-roof scene like Sponza treats the sky as a visible light source.

## Usage

Attach a `FogSettings` to any entity:

```csharp
world.AddComponent(
    world.CreateEntity(),
    new FogSettings
    {
        Color = new Color(180, 190, 200),
        Mode = FogMode.ExponentialSquared,
        Density = 0.02f,
    }
);
```

No further wiring is needed — `MeshRenderSystem` picks it up automatically.

## `FogSettings`

| Field | Meaning |
| --- | --- |
| `Color` | Colour fragments are mixed toward as fog visibility drops. |
| `Mode` | `ExponentialSquared` (default) or `Linear` — see below. |
| `Density` | Thickness used by `ExponentialSquared`: visibility is `exp(-(distance * Density)^2)`. Ignored by `Linear`. |
| `Start` / `End` | Distances `Linear` fog begins/reaches full strength at. Ignored by `ExponentialSquared`. |

`FogSettings.Default` is a light grey `ExponentialSquared` fog (`Density = 0.02`) with a `Start`/
`End` of `10`/`100` for callers that switch to `Linear`.

- **`ExponentialSquared`** has no hard edge and thickens gradually with distance — one `Density`
  knob reads naturally for atmospheric haze without authoring a start/end pair.
- **`Linear`** falls off linearly from fully clear at `Start` to fully fog-coloured at `End` — worth
  having for authored control, e.g. hiding far-plane pop-in at a specific, predictable distance.

## Day/night coupling

A fog colour that warms at the horizon and cools at night is a natural output of a day/night
driver (see [day-night.md](day-night.md)), but that coupling belongs in the driver, not in the
renderer: update the `FogSettings` component's `Color` yourself (e.g. alongside `TimeOfDay`) rather
than expecting `Renderer3D` to derive it.
