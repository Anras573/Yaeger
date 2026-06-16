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
///   "normalTexturePath": null,
///   "ambient": [25, 25, 25],
///   "diffuse": [200, 180, 150],
///   "specular": [255, 255, 255],
///   "shininess": 32.0,
///   "metallicRoughnessTexturePath": null,
///   "aoTexturePath": null,
///   "emissiveTexturePath": null,
///   "metallicFactor": 1.0,
///   "roughnessFactor": 1.0,
///   "emissiveColor": [0, 0, 0]
/// }
/// </code>
/// Texture-path fields are written only when set. Numeric/colour fields default to the
/// <see cref="Material3D()"/> defaults (e.g. metallic/roughness factors of 1.0) when absent.
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
        };
        return (world, entity) => world.AddComponent(entity, component);
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
        if (!string.IsNullOrEmpty(path))
            json[propertyName] = path;
    }
}
