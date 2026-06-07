using Yaeger.Assets;

namespace Yaeger.Graphics;

public record struct Material3D
{
    public string DiffuseTexturePath;
    public string? NormalTexturePath;
    public Color Ambient;
    public Color Diffuse;
    public Color Specular;
    public float Shininess;

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
        };
}
