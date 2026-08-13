using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Tween"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "Tween",
///   "targetTag": "doorLight",
///   "channel": "PointLightIntensity",
///   "from": [0.2, 0.0, 0.0, 0.0],
///   "to": [1.5, 0.0, 0.0, 0.0],
///   "duration": 2.0,
///   "delay": 0.0,
///   "easing": "CubicInOut",
///   "loopMode": "PingPong"
/// }
/// </code>
/// <c>channel</c> and <c>duration</c> are required; <c>from</c>/<c>to</c> default to the zero vector,
/// <c>delay</c> to <c>0</c>, <c>easing</c> to <c>Linear</c>, and <c>loopMode</c> to <c>Once</c>.
/// <c>from</c>/<c>to</c> are always written as 4-element <c>[x, y, z, w]</c> arrays regardless of
/// <c>channel</c> — see <see cref="Tween"/>'s remarks for how each channel packs its value into the
/// unused components.
/// <para>
/// <c>targetTag</c> (optional) is the tag of the entity this tween animates, resolved via
/// <see cref="World.TryGetEntity(string, out Entity)"/> when the component is applied — the same forward-reference
/// convention as <see cref="ParentSerializer"/>'s <c>parentTag</c>. When omitted, the tween targets
/// the entity it is attached to (self-tweening, the common case).
/// </para>
/// </remarks>
public sealed class TweenSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Tween";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Tween);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var channel = ReadRequiredEnum<TweenChannel>(element, "channel");
        var duration = GetRequiredDuration(element);
        var from = ComponentJson.GetOptionalVector4(element, "from", Vector4.Zero);
        var to = ComponentJson.GetOptionalVector4(element, "to", Vector4.Zero);
        var delay = ComponentJson.GetOptionalSingle(element, "delay", 0f);
        var easing = ReadOptionalEnum(element, "easing", EasingFunction.Linear);
        var loopMode = ReadOptionalEnum(element, "loopMode", TweenLoopMode.Once);
        var targetTag = ComponentJson.GetOptionalString(element, "targetTag", null);

        return (world, entity) =>
        {
            var target = entity;
            if (targetTag is not null && !world.TryGetEntity(targetTag, out target))
                throw new PrefabLoadException(
                    $"Tween component on entity {entity.Id} references unknown tag '{targetTag}'. "
                        + "The target entity must already be tagged with that value when this "
                        + "component is applied."
                );

            world.AddComponent(
                entity,
                new Tween(target, channel, from, to, duration, delay, easing, loopMode)
            );
        };
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Tween>(entity, out var tween))
            return null;

        var obj = new JsonObject
        {
            ["type"] = TypeId,
            ["channel"] = tween.Channel.ToString(),
            ["from"] = ComponentJson.Write(tween.From),
            ["to"] = ComponentJson.Write(tween.To),
            ["duration"] = tween.Duration,
        };

        if (tween.Delay != 0f)
            obj["delay"] = tween.Delay;

        if (tween.Easing != EasingFunction.Linear)
            obj["easing"] = tween.Easing.ToString();

        if (tween.LoopMode != TweenLoopMode.Once)
            obj["loopMode"] = tween.LoopMode.ToString();

        if (!tween.Target.Equals(entity))
        {
            if (!world.TryGetTag(tween.Target, out var targetTag))
                throw new InvalidOperationException(
                    $"Entity {entity.Id} has a Tween targeting untagged entity {tween.Target.Id}. "
                        + "Scenes can only express Tween cross-entity references via tags — tag "
                        + "the target entity before saving."
                );

            obj["targetTag"] = targetTag;
        }

        return obj;
    }

    private static float GetRequiredDuration(JsonElement element)
    {
        if (!element.TryGetProperty("duration", out var el))
            throw new PrefabLoadException(
                "Tween component is missing required 'duration' property."
            );

        return ComponentJson.ReadSingle(el, "duration");
    }

    private static T ReadRequiredEnum<T>(JsonElement element, string propertyName)
        where T : struct, Enum
    {
        if (!element.TryGetProperty(propertyName, out var el))
            throw new PrefabLoadException(
                $"Tween component is missing required '{propertyName}' property."
            );

        return ParseEnum<T>(el, propertyName);
    }

    private static T ReadOptionalEnum<T>(JsonElement element, string propertyName, T defaultValue)
        where T : struct, Enum =>
        element.TryGetProperty(propertyName, out var el)
            ? ParseEnum<T>(el, propertyName)
            : defaultValue;

    private static T ParseEnum<T>(JsonElement el, string propertyName)
        where T : struct, Enum
    {
        if (el.ValueKind != JsonValueKind.String)
            throw new PrefabLoadException($"Tween property '{propertyName}' must be a string.");

        var raw = el.GetString();
        if (string.IsNullOrWhiteSpace(raw) || !Enum.TryParse<T>(raw, true, out var value))
            throw new PrefabLoadException(
                $"Tween property '{propertyName}' has unrecognized value '{raw}'."
            );

        return value;
    }
}
