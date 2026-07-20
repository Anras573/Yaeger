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

Base colour and emissive textures are treated as sRGB and linearised before lighting; the final
colour is Reinhard tone-mapped and gamma-encoded back to sRGB.

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
    public bool UsePbr;
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
> used directly, with no linearisation.)

## Loading PBR materials

`AssimpLoader` maps glTF `pbrMetallicRoughness` properties onto `Material3D` automatically. A
material is flagged as PBR when the importer surfaces any metallic/roughness data — glTF always
provides the metallic/roughness factor keys, whereas OBJ/MTL (Blinn-Phong) never does. Existing
Blinn-Phong scenes therefore keep `UsePbr = false` and render exactly as before.

The `Sponza` sample loads the KhronosGroup Sponza glTF and renders it through the PBR path; the
`CornellBox` sample uses hand-authored Blinn-Phong materials.

## Lights

Both shading paths accumulate a directional light plus any point and spot lights in the scene. See
[lighting.md](lighting.md) for the `DirectionalLight`, `PointLight`, and `SpotLight` components and
their falloff behaviour.
