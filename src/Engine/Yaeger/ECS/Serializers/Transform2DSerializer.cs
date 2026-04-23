using System.Numerics;
using System.Text.Json;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Transform2D"/> component.
/// </summary>
/// <remarks>
/// JSON format (vectors as two-element arrays <c>[x, y]</c>):
/// <code>
/// {
///   "type": "Transform2D",
///   "position": [0.0, 0.0],
///   "rotation": 0.0,
///   "scale": [1.0, 1.0]
/// }
/// </code>
/// All properties are optional and default to their zero/identity values when absent.
/// </remarks>
public sealed class Transform2DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Transform2D";

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var position = element.TryGetProperty("position", out var posEl)
            ? ReadVector2(posEl, "position")
            : Vector2.Zero;

        var rotation = element.TryGetProperty("rotation", out var rotEl)
            ? ReadSingle(rotEl, "rotation")
            : 0f;

        var scale = element.TryGetProperty("scale", out var scaleEl)
            ? ReadVector2(scaleEl, "scale")
            : Vector2.One;

        var component = new Transform2D(position, rotation, scale);
        return (world, entity) => world.AddComponent(entity, component);
    }

    private static float ReadSingle(JsonElement el, string propertyName)
    {
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetSingle(out var value))
            throw new PrefabLoadException($"Property '{propertyName}' must be a number.");

        return value;
    }

    private static Vector2 ReadVector2(JsonElement el, string propertyName)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var arr = el.EnumerateArray().ToArray();
            if (arr.Length != 2)
                throw new PrefabLoadException($"Property '{propertyName}' must be a Vector2 array with exactly two numeric elements.");

            return new Vector2(
                ReadSingle(arr[0], $"{propertyName}[0]"),
                ReadSingle(arr[1], $"{propertyName}[1]"));
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            if (!el.TryGetProperty("x", out var xEl))
                throw new PrefabLoadException($"Property '{propertyName}' must contain an 'x' number.");

            if (!el.TryGetProperty("y", out var yEl))
                throw new PrefabLoadException($"Property '{propertyName}' must contain a 'y' number.");

            return new Vector2(
                ReadSingle(xEl, $"{propertyName}.x"),
                ReadSingle(yEl, $"{propertyName}.y"));
        }

        throw new PrefabLoadException($"Property '{propertyName}' must be a Vector2 represented as [x, y] or {{ \"x\": number, \"y\": number }}.");
    }
}
