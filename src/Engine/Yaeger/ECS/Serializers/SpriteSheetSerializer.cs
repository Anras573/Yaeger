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
        var texturePath =
            element.GetProperty("texturePath").GetString()
            ?? throw new PrefabLoadException(
                "SpriteSheet 'texturePath' must be a non-null string."
            );

        var columns = element.GetProperty("columns").GetInt32();

        var rows = element.TryGetProperty("rows", out var rowsEl) ? rowsEl.GetInt32() : 1;

        int? frameCount = element.TryGetProperty("frameCount", out var fcEl)
            ? fcEl.GetInt32()
            : null;

        var component = new SpriteSheet(texturePath, columns, rows, frameCount);
        return (world, entity) => world.AddComponent(entity, component);
    }
}
