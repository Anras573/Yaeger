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
/// { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [255, 0, 0, 255], "flipX": false, "flipY": false }
/// </code>
/// The tint field is optional and defaults to white (255, 255, 255, 255). <c>flipX</c>/<c>flipY</c>
/// are optional and default to <c>false</c>.
/// </remarks>
public sealed class SpriteSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Sprite";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Sprite);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var texturePath = GetRequiredTexturePath(element);
        var tint = ParseOptionalTint(element);
        var flipX = ComponentJson2D.ReadOptionalBool(element, "flipX", false);
        var flipY = ComponentJson2D.ReadOptionalBool(element, "flipY", false);

        var component = new Sprite(texturePath, tint, flipX, flipY);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Sprite>(entity, out var sprite))
            return null;

        var json = new JsonObject { ["type"] = TypeId, ["texturePath"] = sprite.TexturePath };

        WriteTintIfNonDefault(json, sprite.Tint);
        if (sprite.FlipX)
            json["flipX"] = true;
        if (sprite.FlipY)
            json["flipY"] = true;

        return json;
    }

    private static string GetRequiredTexturePath(JsonElement element)
    {
        if (!element.TryGetProperty("texturePath", out var texturePathEl))
            throw new PrefabLoadException(
                "Sprite component is missing required 'texturePath' property."
            );

        if (texturePathEl.ValueKind != JsonValueKind.String)
            throw new PrefabLoadException("Sprite 'texturePath' must be a string.");

        return
            texturePathEl.GetString() is { } texturePath && !string.IsNullOrWhiteSpace(texturePath)
            ? texturePath
            : throw new PrefabLoadException("Sprite 'texturePath' must be a non-empty string.");
    }

    private static Color? ParseOptionalTint(JsonElement element)
    {
        if (!element.TryGetProperty("tint", out var tintEl))
            return null;

        if (tintEl.ValueKind != JsonValueKind.Array)
            throw new PrefabLoadException("Sprite 'tint' must be an array of 3 or 4 numbers.");

        var channels = ReadTintChannels(tintEl);
        var alpha = channels[3];
        return new Color((byte)channels[0], (byte)channels[1], (byte)channels[2], (byte)alpha);
    }

    private static int[] ReadTintChannels(JsonElement tintEl)
    {
        var channels = new int[4];
        var channelCount = 0;
        foreach (var channelEl in tintEl.EnumerateArray())
        {
            EnsureTintChannelCapacity(channelCount, channels.Length);
            channels[channelCount++] = ParseTintChannel(channelEl);
        }

        EnsureMinimumTintChannelCount(channelCount);
        if (channelCount == 3)
            channels[3] = 255;
        return channels;
    }

    private static void EnsureTintChannelCapacity(int channelCount, int maxChannels)
    {
        if (channelCount != maxChannels)
            return;

        throw new PrefabLoadException(
            "Sprite 'tint' array must contain 3 (RGB) or 4 (RGBA) elements."
        );
    }

    private static int ParseTintChannel(JsonElement channelEl)
    {
        if (!channelEl.TryGetInt32(out var channelValue) || channelValue < 0 || channelValue > 255)
            throw new PrefabLoadException(
                "Sprite 'tint' array elements must be integers between 0 and 255."
            );

        return channelValue;
    }

    private static void EnsureMinimumTintChannelCount(int channelCount)
    {
        if (channelCount >= 3)
            return;

        throw new PrefabLoadException(
            "Sprite 'tint' array must contain 3 (RGB) or 4 (RGBA) elements."
        );
    }

    private static void WriteTintIfNonDefault(JsonObject json, Color tint)
    {
        if (tint.R == 255 && tint.G == 255 && tint.B == 255 && tint.A == 255)
            return;

        json["tint"] = new JsonArray(tint.R, tint.G, tint.B, tint.A);
    }
}
