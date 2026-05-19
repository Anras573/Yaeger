using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="SpriteSheet"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "SpriteSheet",
///   "texturePath": "Assets/sheet.png",
///   "columns": 4,
///   "rows": 2,
///   "frameCount": 7,
///   "tint": [255, 0, 0, 255]
/// }
/// </code>
/// <c>rows</c> defaults to <c>1</c>, <c>frameCount</c> defaults to
/// <c>columns * rows</c>, and <c>tint</c> defaults to white when absent.
/// </remarks>
public sealed class SpriteSheetSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "SpriteSheet";

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var texturePath = GetRequiredString(element, "texturePath");
        var columns = GetRequiredPositiveInt(element, "columns");
        var rows = GetOptionalPositiveInt(element, "rows") ?? 1;
        var frameCount = GetOptionalPositiveInt(element, "frameCount");

        int maxFrameCount;
        try
        {
            maxFrameCount = checked(columns * rows);
        }
        catch (OverflowException)
        {
            throw new PrefabLoadException(
                $"SpriteSheet 'columns' ({columns}) and 'rows' ({rows}) produce too many frames."
            );
        }

        if (frameCount.HasValue && frameCount.Value > maxFrameCount)
            throw new PrefabLoadException(
                $"SpriteSheet 'frameCount' ({frameCount.Value}) must not exceed columns * rows ({maxFrameCount})."
            );

        Color? tint = null;
        if (element.TryGetProperty("tint", out var tintEl))
        {
            if (tintEl.ValueKind != JsonValueKind.Array)
                throw new PrefabLoadException(
                    "SpriteSheet 'tint' must be an array of 3 or 4 numbers."
                );

            var channels = new int[4];
            var channelCount = 0;
            foreach (var channelEl in tintEl.EnumerateArray())
            {
                if (channelCount == channels.Length)
                    throw new PrefabLoadException(
                        "SpriteSheet 'tint' array must contain 3 (RGB) or 4 (RGBA) elements."
                    );

                if (
                    !channelEl.TryGetInt32(out var channelValue)
                    || channelValue < 0
                    || channelValue > 255
                )
                    throw new PrefabLoadException(
                        "SpriteSheet 'tint' array elements must be integers between 0 and 255."
                    );

                channels[channelCount++] = channelValue;
            }

            if (channelCount < 3)
                throw new PrefabLoadException(
                    "SpriteSheet 'tint' array must contain 3 (RGB) or 4 (RGBA) elements."
                );

            var alpha = channelCount == 4 ? channels[3] : 255;
            tint = new Color((byte)channels[0], (byte)channels[1], (byte)channels[2], (byte)alpha);
        }

        var component = new SpriteSheet(texturePath, columns, rows, frameCount, tint);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<SpriteSheet>(entity, out var ss))
            return null;

        var obj = new JsonObject
        {
            ["type"] = TypeId,
            ["texturePath"] = ss.TexturePath,
            ["columns"] = ss.Columns,
            ["rows"] = ss.Rows,
        };

        // Only emit frameCount when it differs from the default (columns * rows).
        // Use long arithmetic to avoid overflow for large column/row values.
        if (ss.FrameCount != (long)ss.Columns * ss.Rows)
            obj["frameCount"] = ss.FrameCount;

        if (ss.Tint.R != 255 || ss.Tint.G != 255 || ss.Tint.B != 255 || ss.Tint.A != 255)
        {
            obj["tint"] = new JsonArray(ss.Tint.R, ss.Tint.G, ss.Tint.B, ss.Tint.A);
        }

        return obj;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            throw new PrefabLoadException(
                $"SpriteSheet is missing required '{propertyName}' property."
            );

        if (property.ValueKind != JsonValueKind.String)
            throw new PrefabLoadException($"SpriteSheet '{propertyName}' must be a string.");

        return property.GetString() is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new PrefabLoadException(
                $"SpriteSheet '{propertyName}' must be a non-empty string."
            );
    }

    private static int GetRequiredPositiveInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            throw new PrefabLoadException(
                $"SpriteSheet is missing required '{propertyName}' property."
            );

        if (!property.TryGetInt32(out var value))
            throw new PrefabLoadException($"SpriteSheet '{propertyName}' must be an integer.");

        if (value <= 0)
            throw new PrefabLoadException($"SpriteSheet '{propertyName}' must be greater than 0.");

        return value;
    }

    private static int? GetOptionalPositiveInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (!property.TryGetInt32(out var value))
            throw new PrefabLoadException(
                $"SpriteSheet '{propertyName}' must be an integer when provided."
            );

        if (value <= 0)
            throw new PrefabLoadException(
                $"SpriteSheet '{propertyName}' must be greater than 0 when provided."
            );

        return value;
    }
}
