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
}
