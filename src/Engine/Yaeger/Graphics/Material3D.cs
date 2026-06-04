using Yaeger.Assets;

namespace Yaeger.Graphics;

public record struct Material3D
{
    public string DiffuseTexturePath;
    public Color Ambient;
    public Color Diffuse;
    public Color Specular;
    public float Shininess;

    public static Material3D FromMtl(MtlMaterial mtl) =>
        new()
        {
            DiffuseTexturePath = mtl.DiffuseTexturePath ?? string.Empty,
            Ambient = mtl.AmbientColor,
            Diffuse = mtl.DiffuseColor,
            Specular = mtl.SpecularColor,
            Shininess = mtl.Shininess,
        };

    public static Material3D FromModel(ModelMaterial model) =>
        new()
        {
            DiffuseTexturePath = model.DiffuseTexturePath ?? string.Empty,
            Ambient = Color.Black,
            Diffuse = model.DiffuseColor,
            Specular = Color.Black,
            Shininess = 0f,
        };
}
