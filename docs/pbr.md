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
- A small constant ambient term (`0.03 * albedo * ao`) so unlit faces don't go fully black.
- Emissive contribution added on top.

Base colour and emissive textures are treated as sRGB and linearised before lighting; the final
colour is Reinhard tone-mapped and gamma-encoded back to sRGB.

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
