using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="AmbientLight"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "AmbientLight",
///   "color": [255, 255, 255],
///   "intensity": 0.03
/// }
/// </code>
/// All properties are optional and default to <see cref="AmbientLight.Default"/> when absent.
/// </remarks>
public sealed class AmbientLightSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "AmbientLight";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(AmbientLight);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = AmbientLight.Default;
        var component = new AmbientLight
        {
            Color = ComponentJson.GetOptionalColor(element, "color", defaults.Color),
            Intensity = ComponentJson.GetOptionalSingle(element, "intensity", defaults.Intensity),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<AmbientLight>(entity, out var ambient))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["color"] = ComponentJson.Write(ambient.Color),
            ["intensity"] = ambient.Intensity,
        };
    }
}
