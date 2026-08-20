using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="FogSettings"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "FogSettings",
///   "color": [200, 210, 220],
///   "mode": "ExponentialSquared",
///   "density": 0.02,
///   "start": 10,
///   "end": 100
/// }
/// </code>
/// All properties are optional and default to <see cref="FogSettings.Default"/> when absent.
/// <c>mode</c> is case-insensitive and matches <see cref="FogMode"/>'s member names
/// (<c>"ExponentialSquared"</c> or <c>"Linear"</c>); an unrecognised value falls back to the
/// default mode rather than failing the load, matching the other enum-valued serializers
/// (e.g. <see cref="CelestialLightSerializer"/>).
/// </remarks>
public sealed class FogSettingsSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "FogSettings";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(FogSettings);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = FogSettings.Default;
        var component = new FogSettings
        {
            Color = ComponentJson.GetOptionalColor(element, "color", defaults.Color),
            Mode = ReadOptionalMode(element, defaults.Mode),
            Density = ComponentJson.GetOptionalSingle(element, "density", defaults.Density),
            Start = ComponentJson.GetOptionalSingle(element, "start", defaults.Start),
            End = ComponentJson.GetOptionalSingle(element, "end", defaults.End),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<FogSettings>(entity, out var fog))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["color"] = ComponentJson.Write(fog.Color),
            ["mode"] = fog.Mode.ToString(),
            ["density"] = fog.Density,
            ["start"] = fog.Start,
            ["end"] = fog.End,
        };
    }

    private static FogMode ReadOptionalMode(JsonElement element, FogMode defaultValue)
    {
        var name = ComponentJson.GetOptionalString(element, "mode", null);
        return name != null && Enum.TryParse<FogMode>(name, ignoreCase: true, out var mode)
            ? mode
            : defaultValue;
    }
}
