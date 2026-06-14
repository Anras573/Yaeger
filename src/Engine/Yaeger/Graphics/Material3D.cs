using Yaeger.Assets;

namespace Yaeger.Graphics;

public record struct Material3D
{
    // Blinn-Phong fields (used when UsePbr is false)
    public string DiffuseTexturePath = string.Empty;
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
        };
}
