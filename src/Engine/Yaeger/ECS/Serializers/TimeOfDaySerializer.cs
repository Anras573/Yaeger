using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="TimeOfDay"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "TimeOfDay",
///   "normalizedTime": 0.5,
///   "dayLengthSeconds": 120.0,
///   "northOffset": 0.0,
///   "axisTilt": 0.35
/// }
/// </code>
/// All properties are optional and default to <see cref="TimeOfDay.Default"/> when absent.
/// <see cref="DayNightCycleSettings"/> is deliberately not serialized: it is art direction shared by
/// a whole game rather than per-entity scene data, and it is supplied to
/// <c>DayNightCycleSystem</c> in code.
/// </remarks>
public sealed class TimeOfDaySerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "TimeOfDay";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(TimeOfDay);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = TimeOfDay.Default;
        var component = new TimeOfDay
        {
            NormalizedTime = ComponentJson.GetOptionalSingle(
                element,
                "normalizedTime",
                defaults.NormalizedTime
            ),
            DayLengthSeconds = ComponentJson.GetOptionalSingle(
                element,
                "dayLengthSeconds",
                defaults.DayLengthSeconds
            ),
            NorthOffset = ComponentJson.GetOptionalSingle(
                element,
                "northOffset",
                defaults.NorthOffset
            ),
            AxisTilt = ComponentJson.GetOptionalSingle(element, "axisTilt", defaults.AxisTilt),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<TimeOfDay>(entity, out var time))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["normalizedTime"] = time.NormalizedTime,
            ["dayLengthSeconds"] = time.DayLengthSeconds,
            ["northOffset"] = time.NorthOffset,
            ["axisTilt"] = time.AxisTilt,
        };
    }
}
