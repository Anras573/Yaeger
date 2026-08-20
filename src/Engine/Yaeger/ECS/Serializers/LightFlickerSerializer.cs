using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="LightFlicker"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "LightFlicker",
///   "baseIntensity": 1.0,
///   "amplitude": 0.3,
///   "frequency": 3.0,
///   "seed": 0.0,
///   "positionJitter": 0.0,
///   "elapsed": 0.0
/// }
/// </code>
/// All properties are optional and default to <see cref="LightFlicker.Default"/> when absent.
/// <c>elapsed</c> is the flicker's phase at save time — round-tripping it keeps a reloaded scene's
/// flicker continuous rather than resetting its noise stream to the start.
/// </remarks>
public sealed class LightFlickerSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "LightFlicker";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(LightFlicker);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = LightFlicker.Default;
        var component = new LightFlicker
        {
            BaseIntensity = ComponentJson.GetOptionalSingle(
                element,
                "baseIntensity",
                defaults.BaseIntensity
            ),
            Amplitude = ComponentJson.GetOptionalSingle(element, "amplitude", defaults.Amplitude),
            Frequency = ComponentJson.GetOptionalSingle(element, "frequency", defaults.Frequency),
            Seed = ComponentJson.GetOptionalSingle(element, "seed", defaults.Seed),
            PositionJitter = ComponentJson.GetOptionalSingle(
                element,
                "positionJitter",
                defaults.PositionJitter
            ),
            Elapsed = ComponentJson.GetOptionalSingle(element, "elapsed", defaults.Elapsed),
        };
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<LightFlicker>(entity, out var flicker))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["baseIntensity"] = flicker.BaseIntensity,
            ["amplitude"] = flicker.Amplitude,
            ["frequency"] = flicker.Frequency,
            ["seed"] = flicker.Seed,
            ["positionJitter"] = flicker.PositionJitter,
            ["elapsed"] = flicker.Elapsed,
        };
    }
}
