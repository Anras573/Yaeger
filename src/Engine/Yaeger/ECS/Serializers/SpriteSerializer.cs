using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Sprite"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [255, 0, 0, 255] }
/// </code>
/// The tint field is optional and defaults to white (255, 255, 255, 255).
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

        // Parse optional tint property
        Color? tint = null;
        if (element.TryGetProperty("tint", out var tintEl))
        {
            if (tintEl.ValueKind != JsonValueKind.Array)
                throw new PrefabLoadException("Sprite 'tint' must be an array of 3 or 4 numbers.");

            var channels = new int[4];
            var channelCount = 0;
            foreach (var channelEl in tintEl.EnumerateArray())
            {
                if (channelCount == channels.Length)
                {
                    throw new PrefabLoadException(
                        "Sprite 'tint' array must contain 3 (RGB) or 4 (RGBA) elements."
                    );
                }

                if (
                    !channelEl.TryGetInt32(out var channelValue)
                    || channelValue < 0
                    || channelValue > 255
                )
                {
                    throw new PrefabLoadException(
                        "Sprite 'tint' array elements must be integers between 0 and 255."
                    );
                }

                channels[channelCount++] = channelValue;
            }

            if (channelCount < 3)
                throw new PrefabLoadException(
                    "Sprite 'tint' array must contain 3 (RGB) or 4 (RGBA) elements."
                );

            var alpha = channelCount == 4 ? channels[3] : 255;
            tint = new Color((byte)channels[0], (byte)channels[1], (byte)channels[2], (byte)alpha);
        }

        var component = new Sprite(texturePath, tint);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Sprite>(entity, out var sprite))
            return null;

        var json = new JsonObject { ["type"] = TypeId, ["texturePath"] = sprite.TexturePath };

        // Only serialize tint if it's not white (the default)
        if (
            sprite.Tint.R != 255
            || sprite.Tint.G != 255
            || sprite.Tint.B != 255
            || sprite.Tint.A != 255
        )
        {
            json["tint"] = new JsonArray(
                sprite.Tint.R,
                sprite.Tint.G,
                sprite.Tint.B,
                sprite.Tint.A
            );
        }

        return json;
    }
}
