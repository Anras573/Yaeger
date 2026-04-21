using System.Text.Json;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Sprite"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// { "type": "Sprite", "texturePath": "Assets/ball.png" }
/// </code>
/// </remarks>
public sealed class SpriteSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Sprite";

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        if (!element.TryGetProperty("texturePath", out var texturePathEl))
            throw new PrefabLoadException(
                "Sprite component is missing required 'texturePath' property."
            );

        if (texturePathEl.ValueKind != JsonValueKind.String)
            throw new PrefabLoadException("Sprite 'texturePath' must be a string.");

        var texturePath = texturePathEl.GetString();
        if (string.IsNullOrWhiteSpace(texturePath))
            throw new PrefabLoadException("Sprite 'texturePath' must be a non-empty string.");

        var component = new Sprite(texturePath);
        return (world, entity) => world.AddComponent(entity, component);
    }
}
