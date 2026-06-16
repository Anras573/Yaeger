using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="SpotLight"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "SpotLight",
///   "color": [255, 255, 255],
///   "intensity": 1.0,
///   "direction": [0.0, -1.0, 0.0],
///   "innerConeAngle": 0.349,
///   "outerConeAngle": 0.524,
///   "range": 10.0
/// }
/// </code>
/// The cone angles are half-angles in radians. The light's world position comes from the entity's
/// <see cref="Transform3D"/>. All properties are optional and default to
/// <see cref="SpotLight.Default"/> when absent.
/// </remarks>
public sealed class SpotLightSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "SpotLight";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(SpotLight);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = SpotLight.Default;
        var component = new SpotLight
        {
            Color = ComponentJson.GetOptionalColor(element, "color", defaults.Color),
            Intensity = ComponentJson.GetOptionalSingle(element, "intensity", defaults.Intensity),
            Direction = ComponentJson.GetOptionalVector3(element, "direction", defaults.Direction),
            InnerConeAngle = ComponentJson.GetOptionalSingle(
                element,
                "innerConeAngle",
                defaults.InnerConeAngle
            ),
            OuterConeAngle = ComponentJson.GetOptionalSingle(
                element,
                "outerConeAngle",
                defaults.OuterConeAngle
            ),
            Range = ComponentJson.GetOptionalSingle(element, "range", defaults.Range),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<SpotLight>(entity, out var light))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["color"] = ComponentJson.Write(light.Color),
            ["intensity"] = light.Intensity,
            ["direction"] = ComponentJson.Write(light.Direction),
            ["innerConeAngle"] = light.InnerConeAngle,
            ["outerConeAngle"] = light.OuterConeAngle,
            ["range"] = light.Range,
        };
    }
}
