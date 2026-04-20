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
        var texturePath =
            element.GetProperty("texturePath").GetString()
            ?? throw new PrefabLoadException("Sprite 'texturePath' must be a non-null string.");

        var component = new Sprite(texturePath);
        return (world, entity) => world.AddComponent(entity, component);
    }
}
