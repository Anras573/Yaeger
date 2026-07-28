using Yaeger.Assets;

namespace Yaeger.Graphics;

public record struct Material3D
{
    // Blinn-Phong fields (used when UsePbr is false).
    // Nullable because default(Material3D) bypasses the field initializer and leaves this null;
    // the renderer treats null/empty alike (no diffuse texture).
    public string? DiffuseTexturePath = string.Empty;
    public string? NormalTexturePath;
    public Color Ambient;
    public Color Diffuse;
    public Color Specular;
    public float Shininess;

    // PBR metallic/roughness fields (used when UsePbr is true).
    // The factors default to the glTF 2.0 values (1.0) so that hand-authored PBR materials
    // — e.g. `new Material3D { UsePbr = true, ... }` — behave sensibly without extra boilerplate.
    public string? MetallicRoughnessTexturePath; // glTF packed: G=roughness, B=metallic
    public string? AoTexturePath;
    public string? EmissiveTexturePath;
    public float MetallicFactor = 1f;
    public float RoughnessFactor = 1f;
    public Color EmissiveColor;

    /// <summary>
    /// When true, the renderer shades this material with a Cook-Torrance metallic/roughness
    /// BRDF. When false (the default), it falls back to the legacy Blinn-Phong model so that
    /// hand-authored scenes such as the Cornell Box keep their original appearance.
    /// </summary>
    public bool UsePbr;

    /// <summary>
    /// Alpha factor multiplied into the material's final alpha (on top of the diffuse texture's
    /// own alpha channel). 1 (fully opaque) by default; only meaningful when <see cref="BlendMode"/>
    /// is <see cref="MaterialBlendMode.Cutout"/> or <see cref="MaterialBlendMode.Transparent"/> —
    /// the opaque path ignores alpha entirely, matching pre-transparency rendering.
    /// </summary>
    public float Opacity = 1f;

    /// <summary>
    /// How this material is composited. Defaults to <see cref="MaterialBlendMode.Opaque"/> so
    /// every material predating this field renders exactly as before. See <see cref="MaterialBlendMode"/>
    /// and docs/pbr.md for the rendering behaviour of each mode.
    /// </summary>
    public MaterialBlendMode BlendMode;

    /// <summary>
    /// Alpha threshold below which a fragment is discarded when <see cref="BlendMode"/> is
    /// <see cref="MaterialBlendMode.Cutout"/>. Ignored otherwise. Defaults to 0.5, a common
    /// cutout threshold (e.g. glTF's default <c>alphaCutoff</c>).
    /// </summary>
    public float AlphaCutoff = 0.5f;

    // Required because the PBR factor fields above carry initializers (CS8983). Note this runs
    // for `new Material3D()` / object initializers but not for `default(Material3D)`.
    public Material3D() { }

    public static Material3D FromMtl(MtlMaterial mtl) =>
        new()
        {
            DiffuseTexturePath = mtl.DiffuseTexturePath ?? string.Empty,
            NormalTexturePath = mtl.NormalTexturePath,
            Ambient = mtl.AmbientColor,
            Diffuse = mtl.DiffuseColor,
            Specular = mtl.SpecularColor,
            Shininess = mtl.Shininess,
        };

    // A model's reported opacity below this is treated as "the source format asked for
    // transparency" rather than floating-point noise around a fully-opaque 1.0.
    private const float TransparencyThreshold = 0.999f;

    public static Material3D FromModel(ModelMaterial model) =>
        new()
        {
            DiffuseTexturePath = model.DiffuseTexturePath ?? string.Empty,
            NormalTexturePath = model.NormalTexturePath,
            Ambient = model.AmbientColor,
            Diffuse = model.DiffuseColor,
            Specular = Color.Black,
            Shininess = 0f,
            MetallicRoughnessTexturePath = model.MetallicRoughnessTexturePath,
            AoTexturePath = model.AoTexturePath,
            EmissiveTexturePath = model.EmissiveTexturePath,
            MetallicFactor = model.MetallicFactor,
            RoughnessFactor = model.RoughnessFactor,
            EmissiveColor = model.EmissiveColor,
            UsePbr = model.UsePbr,
            Opacity = model.Opacity,
            BlendMode =
                model.Opacity < TransparencyThreshold
                    ? MaterialBlendMode.Transparent
                    : MaterialBlendMode.Opaque,
        };
}
