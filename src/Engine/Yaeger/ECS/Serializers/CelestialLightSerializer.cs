using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="CelestialLight"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "CelestialLight",
///   "body": "Moon"
/// }
/// </code>
/// <c>body</c> is optional, case-insensitive, and defaults to <see cref="CelestialBody.Sun"/>; an
/// unrecognised value also falls back to the sun rather than failing the load, matching how the
/// other enum-valued serializers treat unknown names.
/// </remarks>
public sealed class CelestialLightSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "CelestialLight";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(CelestialLight);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var name = ComponentJson.GetOptionalString(element, "body", null);
        var body = Enum.TryParse<CelestialBody>(name, ignoreCase: true, out var parsed)
            ? parsed
            : CelestialBody.Sun;

        var component = new CelestialLight(body);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<CelestialLight>(entity, out var celestial))
            return null;

        return new JsonObject { ["type"] = TypeId, ["body"] = celestial.Body.ToString() };
    }
}
