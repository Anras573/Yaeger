using Yaeger.Graphics;

namespace Yaeger.Assets;

public record ModelMaterial(
    string Name,
    string? DiffuseTexturePath,
    string? NormalTexturePath,
    Color DiffuseColor
);
