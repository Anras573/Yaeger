using Yaeger.Graphics;

namespace Yaeger.Assets;

public record ModelMaterial(
    string Name,
    string? DiffuseTexturePath,
    string? NormalTexturePath,
    Color DiffuseColor,
    Color AmbientColor,
    string? MetallicRoughnessTexturePath = null,
    string? AoTexturePath = null,
    string? EmissiveTexturePath = null,
    float MetallicFactor = 1f,
    float RoughnessFactor = 1f,
    Color EmissiveColor = default,
    bool UsePbr = false
);
