using System.Text.Json;
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
///   "frameCount": 7
/// }
/// </code>
/// <c>rows</c> defaults to <c>1</c> and <c>frameCount</c> defaults to
/// <c>columns * rows</c> when absent.
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

        if (frameCount.HasValue && frameCount.Value > columns * rows)
            throw new PrefabLoadException(
                $"SpriteSheet 'frameCount' ({frameCount.Value}) must not exceed columns * rows ({columns * rows})."
            );

        var component = new SpriteSheet(texturePath, columns, rows, frameCount);
        return (world, entity) => world.AddComponent(entity, component);
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
