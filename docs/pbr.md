# Physically-Based Rendering (PBR)

`Renderer3D` supports two shading models, selected per material:

- **Blinn-Phong** (the default) — the legacy model used by hand-authored scenes such as
  the Cornell Box.
- **PBR metallic/roughness** — a Cook-Torrance BRDF matching the glTF 2.0 material model.

The model is chosen by the `Material3D.UsePbr` flag. The same shader program implements both
paths and branches on a `uUsePbr` uniform, so mixing PBR and Blinn-Phong entities in one scene
works without swapping shaders.

## The Cook-Torrance BRDF

The PBR path implements:

- **Diffuse**: Lambertian, weighted by `(1 - metallic)`.
- **Specular**: GGX normal-distribution term, Smith geometry term, and a Schlick Fresnel
  approximation (`F0 = 0.04` for dielectrics, lerped toward the albedo for metals).
- **Ambient**: a small constant term (`0.03 * albedo * ao`) so unlit faces don't go fully black —
  unless a skybox has been [prefiltered for image-based lighting](#image-based-lighting), in which
  case ambient comes from the environment instead.
- Emissive contribution added on top.

Base colour and emissive textures are treated as sRGB and linearised before lighting. What happens
to the final linear colour depends on `Renderer3D`'s `hdrOutput` constructor flag (default `false`):
Reinhard-tone-mapped and gamma-encoded back to sRGB in-shader, right here, when `false` (the
original behaviour); left as unclamped linear HDR when `true`, for a `PostProcessStack`'s
`ToneMapEffect` to compress and gamma-encode once, later, over the whole frame instead. See
[HDR and tone mapping](#hdr-and-tone-mapping) below and docs/post-processing.md.

## HDR and tone mapping

`Color` channels are byte-based (`R`/`G`/`B`/`A` each `[0, 255]` → `[0, 1]`), so `EmissiveColor` on
its own can never author a surface brighter than diffuse white — the whole point of an emissive
material meant to read as a light source (a glowing blade, a hot filament, a bolt core).
`Material3D.EmissiveIntensity` (default `1`, no change) is the multiplier that gets it there:
`emissive = EmissiveColor.rgb * EmissiveIntensity`, so `EmissiveIntensity = 4` authors a surface
four times brighter than white.

That only has a *visible* effect beyond flat white when the value survives past this shader. Three
things have to line up (see docs/post-processing.md's [HDR and tone mapping](post-processing.md#hdr-and-tone-mapping)
section for the full picture):

1. `Renderer3D` constructed with `hdrOutput: true`, so the PBR path skips its in-shader Reinhard
   tone-map/gamma-encode and writes linear HDR colour instead (see above).
2. A `PostProcessStack` constructed with `hdr: true`, so its scene/ping-pong targets are allocated
   `Rgba16F` instead of the original 8-bit `Rgba8` and don't clamp the over-1.0 value away the
   instant it's written.
3. A `ToneMapEffect` as the *last* effect in that stack's chain, to compress the HDR result back
   down to the backbuffer's displayable `[0, 1]` range and gamma-encode it.

Skip any one of the three and the emissive value clamps to flat white somewhere along the way —
which is exactly what happens by default (`hdrOutput: false`, no `hdr: true`, no `ToneMapEffect`),
keeping every scene predating this feature pixel-identical to before it existed. The Blinn-Phong
path is unaffected by any of this either way — it was never gamma-encoded in-shader to begin with
(see the colour-space note under [Material3D PBR fields](#material3d-pbr-fields) below), so its
values are unclamped by `Renderer3D` regardless of `hdrOutput`, but combining Blinn-Phong materials
with an HDR chain has not been specifically color-graded for and may look different than the
LDR path once `ToneMapEffect`'s gamma-encode runs over them.

## Image-Based Lighting

By default the PBR path's ambient term is the flat constant above — it doesn't know what's around
the object. `IblPrefilter` + `EnvironmentMapRegistry` replace it with real environment lighting
derived from the scene's skybox: PBR materials pick up diffuse light *and* specular reflections
from the sky, with reflections blurring as roughness increases. This is **opt-in**: a scene that
never creates these two objects renders exactly as it did before this feature existed, and the
Blinn-Phong path is unaffected either way.

### How it works

`IblPrefilter` runs three one-off offscreen GPU passes per skybox (following the same
"renderer owns its FBO state" pattern as `ShadowMapRenderer`), each drawing a unit cube from its
centre into a cubemap-face framebuffer attachment — the same technique `SkyboxRenderer` uses to
display the sky, just capturing into a texture instead of the screen:

1. **Irradiance convolution** — a small (32²) cubemap holding the cosine-weighted diffuse
   irradiance for every direction, used directly as the diffuse ambient term.
2. **Specular prefilter** — a mip-chained (128² base, 5 levels) cubemap where each mip holds the
   environment pre-convolved with the GGX lobe for that mip's roughness (mip 0 = sharp mirror, the
   last mip = fully rough). The lighting pass picks a mip from the surface's roughness.
3. **Split-sum BRDF LUT** — a 128×128 2D texture integrating the specular BRDF's scale/bias over
   `(NdotV, roughness)` (Karis, *Real Shading in Unreal Engine 4*). It depends only on the BRDF,
   not the environment, so it's computed once (lazily, on the first `Prefilter` call) and shared
   across every skybox.

`EnvironmentMapRegistry.Register(skybox, viewportWidth, viewportHeight)` runs these passes for a
skybox already registered with a `CubemapRegistry` and stores the resulting `EnvironmentMap`.
Prefiltering happens **once**, at registration — a skybox swapped at runtime is not
re-prefiltered automatically (out of scope for now; register the new skybox again if you need
that).

### Usage

```csharp
using var cubemaps = new CubemapRegistry(window.Gl);
var skybox = cubemaps.Register(right, left, top, bottom, front, back);

using var iblPrefilter = new IblPrefilter(window.Gl);
using var environmentMaps = new EnvironmentMapRegistry(cubemaps, iblPrefilter);
environmentMaps.Register(skybox, (int)window.Size.X, (int)window.Size.Y);

using var renderer3D = new Renderer3D(window.Gl);
var meshRenderSystem = new MeshRenderSystem(
    renderer3D, meshRegistry, textures, world, window,
    skyboxRenderer: skyboxRenderer,
    cubemapRegistry: cubemaps,
    environmentMaps: environmentMaps);
```

That's the whole wiring — each frame, `MeshRenderSystem` finds the first `Skybox` entity, looks up
its `EnvironmentMap` in the registry, and calls `Renderer3D.SetEnvironmentMap`/`DisableIBL`
automatically. A scene with a `Skybox` entity but no matching registration (or no
`EnvironmentMapRegistry` passed at all) simply keeps the flat ambient term — the same as before
this feature existed.

### Colour space

Skybox cubemaps are loaded as raw, untagged 8-bit textures (see `CubemapTexture`), so
`SkyboxRenderer` can display them with no conversion. `IblPrefilter`'s convolution shaders
linearise (`pow(rgb, 2.2)`) each source sample before integrating it, and write the convolved
result back already linear — so `Renderer3D`'s PBR path samples the irradiance/prefiltered maps
directly, with no further linearisation, the same way it already treats `Lo`/`emissive`.

### Limitations

- Prefiltering is a one-off GPU cost per `Register` call — expect it to take noticeably longer than
  a single frame; call it during scene setup, not every frame.
- All three textures are LDR (8-bit `RGBA`), matching the engine's LDR skybox/texture pipeline —
  there's no HDR skybox support yet to prefilter from.
- Local reflection probes, parallax-corrected cubemaps, and screen-space reflections are out of
  scope; this is a single environment for the whole scene.

## `Material3D` PBR fields

```csharp
public record struct Material3D
{
    // Blinn-Phong (used when UsePbr is false)
    public string? DiffuseTexturePath = string.Empty;
    public string? NormalTexturePath;
    public Color Ambient;
    public Color Diffuse;
    public Color Specular;
    public float Shininess;

    // PBR metallic/roughness (used when UsePbr is true)
    public string? MetallicRoughnessTexturePath; // glTF packed: G=roughness, B=metallic
    public string? AoTexturePath;
    public string? EmissiveTexturePath;
    public float MetallicFactor = 1f;   // scales the texture's metallic channel
    public float RoughnessFactor = 1f;  // scales the texture's roughness channel
    public Color EmissiveColor;
    public float EmissiveIntensity = 1f; // multiplies EmissiveColor; >1 authors above-white emissive
    public bool UsePbr;

    // Transparency (either shading path — see "Transparency" below)
    public float Opacity = 1f;
    public MaterialBlendMode BlendMode;    // Opaque (default), Cutout, or Transparent
    public float AlphaCutoff = 0.5f;       // Cutout only
}
```

`Diffuse` doubles as the glTF base-colour factor in the PBR path. When a metallic/roughness
texture is present, glTF packs roughness in the green channel and metallic in the blue channel;
the `MetallicFactor`/`RoughnessFactor` scalars multiply those channels (and stand in for them when
no texture is bound).

> **Colour space**: in the PBR path the `Diffuse` (base-colour) and `EmissiveColor` factors are
> treated as **linear**, matching glTF 2.0, whereas the base-colour and emissive *textures* are
> assumed to be sRGB and are linearised before being multiplied by the factors. When authoring PBR
> materials by hand, pick `Color` factor values in linear space — an sRGB-intended value (e.g. a
> mid-grey `128`) will render brighter than expected. (In the Blinn-Phong path these colours are
> used directly, with no linearisation.) Because `Color` channels are byte-based, `EmissiveColor`
> itself tops out at linear white (`1.0`) — `EmissiveIntensity` is the multiplier that goes beyond
> it; see [HDR and tone mapping](#hdr-and-tone-mapping) above for what it takes for that extra
> brightness to actually reach the screen instead of clamping away.

## Loading PBR materials

`AssimpLoader` maps glTF `pbrMetallicRoughness` properties onto `Material3D` automatically. A
material is flagged as PBR when the importer surfaces any metallic/roughness data — glTF always
provides the metallic/roughness factor keys, whereas OBJ/MTL (Blinn-Phong) never does. Existing
Blinn-Phong scenes therefore keep `UsePbr = false` and render exactly as before.

The `Sponza` sample loads the KhronosGroup Sponza glTF and renders it through the PBR path; the
`CornellBox` sample uses hand-authored Blinn-Phong materials.

## Transparency

`MaterialBlendMode` selects how a material is composited, independent of the Blinn-Phong/PBR
shading choice above:

- **`Opaque`** (default) — depth write on, fully opaque. Every material predating this feature
  defaults here, so existing scenes render byte-identical to before it existed.
- **`Cutout`** — still drawn in the main (depth-write-on) pass, but a fragment whose final alpha
  falls below `AlphaCutoff` (default 0.5) is `discard`ed in the fragment shader rather than
  blended. No sorting is needed since nothing is actually blended. Suited to foliage, chain-link
  fences, and similar alpha-tested geometry (glTF's `alphaMode: MASK`).
- **`Transparent`** — drawn in a second pass, after every opaque/cutout material, sorted
  back-to-front by view-space depth (see `TransparencySorter`), with depth *testing* on but depth
  *writes* off. Suited to glass, water, and other alpha-blended surfaces (glTF's
  `alphaMode: BLEND`).

`Opacity` is an extra alpha factor multiplied into the diffuse texture's own alpha channel (and,
in the PBR path, `Diffuse`'s alpha); it's ignored by the `Opaque` path (which never reads alpha),
so setting it alone does nothing unless `BlendMode` is also `Cutout` or `Transparent`.

`AssimpLoader` populates `Opacity` from the source material's opacity factor (glTF's
`baseColorFactor` alpha, or an OBJ/FBX transparency factor, when the importer surfaces one) and
sets `BlendMode = Transparent` whenever that opacity is below ~1. There's currently no
importer-driven `Cutout`/`alphaMode: MASK` detection — set `BlendMode`/`AlphaCutoff` by hand for
cutout materials (`Material3D.FromModel(...) with { BlendMode = MaterialBlendMode.Cutout }`).

**Limitations** (see issue #149's scope): this is standard per-object back-to-front sorting, not
order-independent transparency — two *overlapping* transparent objects sort correctly by their
whole-object depth, but a single non-convex transparent mesh can still show sorting artifacts
against itself (per-triangle sorting is out of scope). There's no refraction. Transparent
materials receive the same lighting and shadow *receiving* as opaque ones, but do not themselves
cast shadows — the shadow pre-pass skips any `Transparent`-blend-mode entity entirely (`Cutout`
casts a full, non-masked shadow).

## Lights

Both shading paths accumulate a directional light plus any point and spot lights in the scene. See
[lighting.md](lighting.md) for the `DirectionalLight`, `PointLight`, and `SpotLight` components and
their falloff behaviour.
