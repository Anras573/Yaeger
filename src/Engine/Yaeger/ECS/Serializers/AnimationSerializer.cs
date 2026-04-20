using System.Text.Json;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Animation"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "Animation",
///   "loop": true,
///   "frames": [
///     { "texturePath": "Assets/frame0.png", "duration": 0.1 },
///     { "texturePath": "Assets/frame1.png", "duration": 0.1 }
///   ]
/// }
/// </code>
/// <c>loop</c> defaults to <c>true</c> when absent.
/// </remarks>
public sealed class AnimationSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Animation";

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var loop = !element.TryGetProperty("loop", out var loopEl) || loopEl.GetBoolean();

        if (
            !element.TryGetProperty("frames", out var framesEl)
            || framesEl.ValueKind != JsonValueKind.Array
        )
            throw new PrefabLoadException("Animation 'frames' must be a non-empty JSON array.");

        var framesArray = framesEl.EnumerateArray().ToArray();
        if (framesArray.Length == 0)
            throw new PrefabLoadException("Animation 'frames' must contain at least one entry.");

        var frames = new AnimationFrame[framesArray.Length];
        for (var i = 0; i < framesArray.Length; i++)
        {
            var frameEl = framesArray[i];

            var texturePath =
                frameEl.GetProperty("texturePath").GetString()
                ?? throw new PrefabLoadException(
                    $"Animation frame {i} 'texturePath' must be a non-null string."
                );

            var duration = frameEl.GetProperty("duration").GetSingle();
            frames[i] = new AnimationFrame(texturePath, duration);
        }

        var component = new Animation(frames, loop);
        return (world, entity) => world.AddComponent(entity, component);
    }
}
