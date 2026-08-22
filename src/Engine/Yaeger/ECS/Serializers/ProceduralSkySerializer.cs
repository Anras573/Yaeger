using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="ProceduralSky"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "ProceduralSky",
///   "sunDirection": [0, 1, 0],
///   "moonDirection": [0, -1, 0],
///   "daylightFactor": 1.0,
///   "cloudWind": [0.015, 0.01],
///   "cloudScale": 2.5,
///   "cloudCoverage": 0.45,
///   "starDensity": 0.985,
///   "moonPhase": 0.5,
///   "elapsed": 0.0
/// }
/// </code>
/// All properties are optional and default to <see cref="ProceduralSky.Default"/> when absent.
/// <c>sunDirection</c>/<c>moonDirection</c>/<c>daylightFactor</c> are normally overwritten every
/// update by <c>DayNightCycleSystem</c> when a <see cref="TimeOfDay"/> shares the world (see
/// <see cref="ProceduralSky"/>'s remarks) — the serialized values just give the scene a sane look
/// before the first update, same as a serialized <see cref="DirectionalLight"/> does.
/// </remarks>
public sealed class ProceduralSkySerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "ProceduralSky";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(ProceduralSky);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = ProceduralSky.Default;
        var component = new ProceduralSky
        {
            SunDirection = ComponentJson.GetOptionalVector3(
                element,
                "sunDirection",
                defaults.SunDirection
            ),
            MoonDirection = ComponentJson.GetOptionalVector3(
                element,
                "moonDirection",
                defaults.MoonDirection
            ),
            DaylightFactor = ComponentJson.GetOptionalSingle(
                element,
                "daylightFactor",
                defaults.DaylightFactor
            ),
            CloudWind = GetOptionalVector2(element, "cloudWind", defaults.CloudWind),
            CloudScale = ComponentJson.GetOptionalSingle(
                element,
                "cloudScale",
                defaults.CloudScale
            ),
            CloudCoverage = ComponentJson.GetOptionalSingle(
                element,
                "cloudCoverage",
                defaults.CloudCoverage
            ),
            StarDensity = ComponentJson.GetOptionalSingle(
                element,
                "starDensity",
                defaults.StarDensity
            ),
            MoonPhase = ComponentJson.GetOptionalSingle(element, "moonPhase", defaults.MoonPhase),
            Elapsed = ComponentJson.GetOptionalSingle(element, "elapsed", defaults.Elapsed),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<ProceduralSky>(entity, out var sky))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["sunDirection"] = ComponentJson.Write(sky.SunDirection),
            ["moonDirection"] = ComponentJson.Write(sky.MoonDirection),
            ["daylightFactor"] = sky.DaylightFactor,
            ["cloudWind"] = ComponentJson2D.Write(sky.CloudWind),
            ["cloudScale"] = sky.CloudScale,
            ["cloudCoverage"] = sky.CloudCoverage,
            ["starDensity"] = sky.StarDensity,
            ["moonPhase"] = sky.MoonPhase,
            ["elapsed"] = sky.Elapsed,
        };
    }

    private static Vector2 GetOptionalVector2(
        JsonElement element,
        string propertyName,
        Vector2 defaultValue
    ) =>
        element.TryGetProperty(propertyName, out var el)
            ? ComponentJson2D.ReadVector2(el, propertyName)
            : defaultValue;
}
