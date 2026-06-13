using Yaeger.Assets;

namespace Yaeger.Graphics;

public record struct Material3D
{
    // Blinn-Phong fields (used when UsePbr is false)
    public string DiffuseTexturePath;
    public string? NormalTexturePath;
    public Color Ambient;
    public Color Diffuse;
    public Color Specular;
    public float Shininess;

    // PBR metallic/roughness fields (used when UsePbr is true)
    public string? MetallicRoughnessTexturePath; // glTF packed: G=roughness, B=metallic
    public string? AoTexturePath;
    public string? EmissiveTexturePath;
    public float MetallicFactor;
    public float RoughnessFactor;
    public Color EmissiveColor;

    /// <summary>
    /// When true, the renderer shades this material with a Cook-Torrance metallic/roughness
    /// BRDF. When false (the default), it falls back to the legacy Blinn-Phong model so that
    /// hand-authored scenes such as the Cornell Box keep their original appearance.
    /// </summary>
    public bool UsePbr;

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
