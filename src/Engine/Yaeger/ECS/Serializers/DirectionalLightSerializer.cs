using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="DirectionalLight"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "DirectionalLight",
///   "direction": [0.0, 1.0, 0.0],
///   "color": [255, 255, 255],
///   "intensity": 1.0
/// }
/// </code>
/// All properties are optional and default to <see cref="DirectionalLight.Default"/> when absent.
/// </remarks>
public sealed class DirectionalLightSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "DirectionalLight";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(DirectionalLight);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = DirectionalLight.Default;
        var component = new DirectionalLight
        {
            Direction = ComponentJson.GetOptionalVector3(element, "direction", defaults.Direction),
            Color = ComponentJson.GetOptionalColor(element, "color", defaults.Color),
            Intensity = ComponentJson.GetOptionalSingle(element, "intensity", defaults.Intensity),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<DirectionalLight>(entity, out var light))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["direction"] = ComponentJson.Write(light.Direction),
            ["color"] = ComponentJson.Write(light.Color),
            ["intensity"] = light.Intensity,
        };
    }
}
