using Yaeger.Graphics;

namespace Yaeger.Assets;

public record MtlMaterial(
    string Name,
    string? DiffuseTexturePath,
    Color AmbientColor,
    Color DiffuseColor,
    Color SpecularColor,
    float Shininess
);
