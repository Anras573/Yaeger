using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Material3D"/> component.
/// </summary>
/// <remarks>
/// JSON format (all properties optional):
/// <code>
/// {
///   "type": "Material3D",
///   "usePbr": false,
///   "diffuseTexturePath": "Assets/wood.png",
///   "ambient": [25, 25, 25],
///   "diffuse": [200, 180, 150],
///   "specular": [255, 255, 255],
///   "shininess": 32.0,
///   "metallicFactor": 1.0,
///   "roughnessFactor": 1.0,
///   "emissiveColor": [0, 0, 0],
///   "emissiveIntensity": 1.0,
///   "opacity": 1.0,
///   "blendMode": "Opaque",
///   "alphaCutoff": 0.5
/// }
/// </code>
/// Texture-path fields (<c>diffuseTexturePath</c>, <c>normalTexturePath</c>,
/// <c>metallicRoughnessTexturePath</c>, <c>aoTexturePath</c>, <c>emissiveTexturePath</c>) are
/// written only when set, so unset ones are omitted entirely rather than emitted as <c>null</c>.
/// Numeric/colour fields default to the <see cref="Material3D()"/> defaults (e.g. metallic/roughness
/// factors of 1.0) when absent. <c>blendMode</c> must be one of <c>"Opaque"</c>, <c>"Cutout"</c>,
/// <c>"Transparent"</c>, or <c>"Additive"</c> (matching <see cref="MaterialBlendMode"/>) when
/// present, and defaults to <c>"Opaque"</c> when absent.
/// </remarks>
public sealed class Material3DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Material3D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Material3D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        // Start from the parameterless-constructor defaults (glTF-style factors of 1.0, empty
        // diffuse path) so absent properties keep sensible values rather than zeroes.
        var defaults = new Material3D();
        var component = new Material3D
        {
            UsePbr = ComponentJson.GetOptionalBoolean(element, "usePbr", defaults.UsePbr),
            DiffuseTexturePath = ComponentJson.GetOptionalString(
                element,
                "diffuseTexturePath",
                defaults.DiffuseTexturePath
            ),
            NormalTexturePath = ComponentJson.GetOptionalString(
                element,
                "normalTexturePath",
                defaults.NormalTexturePath
            ),
            Ambient = ComponentJson.GetOptionalColor(element, "ambient", defaults.Ambient),
            Diffuse = ComponentJson.GetOptionalColor(element, "diffuse", defaults.Diffuse),
            Specular = ComponentJson.GetOptionalColor(element, "specular", defaults.Specular),
            Shininess = ComponentJson.GetOptionalSingle(element, "shininess", defaults.Shininess),
            MetallicRoughnessTexturePath = ComponentJson.GetOptionalString(
                element,
                "metallicRoughnessTexturePath",
                defaults.MetallicRoughnessTexturePath
            ),
            AoTexturePath = ComponentJson.GetOptionalString(
                element,
                "aoTexturePath",
                defaults.AoTexturePath
            ),
            EmissiveTexturePath = ComponentJson.GetOptionalString(
                element,
                "emissiveTexturePath",
                defaults.EmissiveTexturePath
            ),
            MetallicFactor = ComponentJson.GetOptionalSingle(
                element,
                "metallicFactor",
                defaults.MetallicFactor
            ),
            RoughnessFactor = ComponentJson.GetOptionalSingle(
                element,
                "roughnessFactor",
                defaults.RoughnessFactor
            ),
            EmissiveColor = ComponentJson.GetOptionalColor(
                element,
                "emissiveColor",
                defaults.EmissiveColor
            ),
            EmissiveIntensity = ComponentJson.GetOptionalSingle(
                element,
                "emissiveIntensity",
                defaults.EmissiveIntensity
            ),
            Opacity = ComponentJson.GetOptionalSingle(element, "opacity", defaults.Opacity),
            BlendMode = ReadOptionalBlendMode(element, defaults.BlendMode),
            AlphaCutoff = ComponentJson.GetOptionalSingle(
                element,
                "alphaCutoff",
                defaults.AlphaCutoff
            ),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    private static MaterialBlendMode ReadOptionalBlendMode(
        JsonElement element,
        MaterialBlendMode defaultValue
    )
    {
        if (!element.TryGetProperty("blendMode", out var el))
            return defaultValue;

        if (
            el.ValueKind != JsonValueKind.String
            || !Enum.TryParse<MaterialBlendMode>(el.GetString(), out var blendMode)
            || !Enum.IsDefined(blendMode)
        )
            throw new PrefabLoadException(
                "Property 'blendMode' must be one of \"Opaque\", \"Cutout\", \"Transparent\", or \"Additive\"."
            );

        return blendMode;
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Material3D>(entity, out var m))
            return null;

        var json = new JsonObject
        {
            ["type"] = TypeId,
            ["usePbr"] = m.UsePbr,
            ["ambient"] = ComponentJson.Write(m.Ambient),
            ["diffuse"] = ComponentJson.Write(m.Diffuse),
            ["specular"] = ComponentJson.Write(m.Specular),
            ["shininess"] = m.Shininess,
            ["metallicFactor"] = m.MetallicFactor,
            ["roughnessFactor"] = m.RoughnessFactor,
            ["emissiveColor"] = ComponentJson.Write(m.EmissiveColor),
            ["emissiveIntensity"] = m.EmissiveIntensity,
            ["opacity"] = m.Opacity,
            ["blendMode"] = m.BlendMode.ToString(),
            ["alphaCutoff"] = m.AlphaCutoff,
        };

        WriteTexturePathIfSet(json, "diffuseTexturePath", m.DiffuseTexturePath);
        WriteTexturePathIfSet(json, "normalTexturePath", m.NormalTexturePath);
        WriteTexturePathIfSet(json, "metallicRoughnessTexturePath", m.MetallicRoughnessTexturePath);
        WriteTexturePathIfSet(json, "aoTexturePath", m.AoTexturePath);
        WriteTexturePathIfSet(json, "emissiveTexturePath", m.EmissiveTexturePath);

        return json;
    }

    private static void WriteTexturePathIfSet(JsonObject json, string propertyName, string? path)
    {
        // Treat whitespace-only paths as unset so we never serialize an invalid asset reference
        // that downstream consumers (which only check IsNullOrEmpty) would load.
        if (!string.IsNullOrWhiteSpace(path))
            json[propertyName] = path;
    }
}
