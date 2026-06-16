using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="PointLight"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "PointLight",
///   "color": [255, 255, 255],
///   "intensity": 1.0,
///   "range": 10.0
/// }
/// </code>
/// The light's world position comes from the entity's <see cref="Transform3D"/>, so it is not
/// stored here. All properties are optional and default to <see cref="PointLight.Default"/> when
/// absent.
/// </remarks>
public sealed class PointLightSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "PointLight";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(PointLight);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = PointLight.Default;
        var component = new PointLight
        {
            Color = ComponentJson.GetOptionalColor(element, "color", defaults.Color),
            Intensity = ComponentJson.GetOptionalSingle(element, "intensity", defaults.Intensity),
            Range = ComponentJson.GetOptionalSingle(element, "range", defaults.Range),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<PointLight>(entity, out var light))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["color"] = ComponentJson.Write(light.Color),
            ["intensity"] = light.Intensity,
            ["range"] = light.Range,
        };
    }
}
