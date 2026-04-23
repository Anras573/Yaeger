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
        var loop = true;
        if (element.TryGetProperty("loop", out var loopEl))
        {
            if (loopEl.ValueKind != JsonValueKind.True && loopEl.ValueKind != JsonValueKind.False)
                throw new PrefabLoadException("Animation 'loop' must be a JSON boolean.");

            loop = loopEl.GetBoolean();
        }
        if (!element.TryGetProperty("frames", out var framesEl))
            throw new PrefabLoadException("Animation is missing required 'frames' property.");

        if (framesEl.ValueKind != JsonValueKind.Array)
            throw new PrefabLoadException("Animation 'frames' must be a JSON array.");
        var framesArray = framesEl.EnumerateArray().ToArray();
        if (framesArray.Length == 0)
            throw new PrefabLoadException("Animation 'frames' must contain at least one entry.");

        var frames = new AnimationFrame[framesArray.Length];
        for (var i = 0; i < framesArray.Length; i++)
        {
            var frameEl = framesArray[i];

            if (frameEl.ValueKind != JsonValueKind.Object)
                throw new PrefabLoadException($"Animation frame {i} must be a JSON object.");

            if (!frameEl.TryGetProperty("texturePath", out var texturePathEl))
                throw new PrefabLoadException(
                    $"Animation frame {i} is missing required 'texturePath' property."
                );

            if (texturePathEl.ValueKind != JsonValueKind.String)
                throw new PrefabLoadException(
                    $"Animation frame {i} 'texturePath' must be a string."
                );

            var texturePath = texturePathEl.GetString();
            if (string.IsNullOrWhiteSpace(texturePath))
                throw new PrefabLoadException(
                    $"Animation frame {i} 'texturePath' must be a non-empty string."
                );

            if (!frameEl.TryGetProperty("duration", out var durationEl))
                throw new PrefabLoadException(
                    $"Animation frame {i} is missing required 'duration' property."
                );

            if (durationEl.ValueKind != JsonValueKind.Number)
                throw new PrefabLoadException(
                    $"Animation frame {i} 'duration' must be a JSON number."
                );

            if (!durationEl.TryGetSingle(out var duration))
                throw new PrefabLoadException(
                    $"Animation frame {i} 'duration' must be a valid number."
                );

            if (duration <= 0)
                throw new PrefabLoadException(
                    $"Animation frame {i} 'duration' must be greater than 0."
                );

            frames[i] = new AnimationFrame(texturePath, duration);
        }

        var component = new Animation(frames, loop);
        return (world, entity) => world.AddComponent(entity, component);
    }
}
