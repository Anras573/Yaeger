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
        var frameIndex = element.TryGetProperty("currentFrameIndex", out var fiEl)
            ? fiEl.GetInt32()
            : 0;

        var elapsedTime = element.TryGetProperty("elapsedTime", out var etEl)
            ? etEl.GetSingle()
            : 0f;

        var isFinished = element.TryGetProperty("isFinished", out var ifEl) && ifEl.GetBoolean();

        var component = new AnimationState(frameIndex, elapsedTime, isFinished);
        return (world, entity) => world.AddComponent(entity, component);
    }
}
