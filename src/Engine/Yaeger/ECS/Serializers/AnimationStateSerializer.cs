using System.Text.Json;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="AnimationState"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "AnimationState",
///   "currentFrameIndex": 0,
///   "elapsedTime": 0.0,
///   "isFinished": false
/// }
/// </code>
/// All properties are optional and default to their zero values when absent.
/// </remarks>
public sealed class AnimationStateSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "AnimationState";

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var frameIndex = GetOptionalInt32(element, "currentFrameIndex", 0);
        var elapsedTime = GetOptionalSingle(element, "elapsedTime", 0f);
        var isFinished = GetOptionalBoolean(element, "isFinished", false);

        var component = new AnimationState(frameIndex, elapsedTime, isFinished);
        return (world, entity) => world.AddComponent(entity, component);
    }

    private static int GetOptionalInt32(JsonElement element, string propertyName, int defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            throw new PrefabLoadException($"AnimationState.{propertyName} must be an integer.");
        }

        return value;
    }

    private static float GetOptionalSingle(
        JsonElement element,
        string propertyName,
        float defaultValue
    )
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetSingle(out var value))
        {
            throw new PrefabLoadException($"AnimationState.{propertyName} must be a number.");
        }

        return value;
    }

    private static bool GetOptionalBoolean(
        JsonElement element,
        string propertyName,
        bool defaultValue
    )
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
        {
            throw new PrefabLoadException($"AnimationState.{propertyName} must be a boolean.");
        }

        return property.GetBoolean();
    }
}
