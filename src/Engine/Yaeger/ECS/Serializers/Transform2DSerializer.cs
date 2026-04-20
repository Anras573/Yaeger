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
            ? ReadVector2(posEl)
            : Vector2.Zero;

        var rotation = element.TryGetProperty("rotation", out var rotEl) ? rotEl.GetSingle() : 0f;

        var scale = element.TryGetProperty("scale", out var scaleEl)
            ? ReadVector2(scaleEl)
            : Vector2.One;

        var component = new Transform2D(position, rotation, scale);
        return (world, entity) => world.AddComponent(entity, component);
    }

    private static Vector2 ReadVector2(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            var arr = el.EnumerateArray().ToArray();
            if (arr.Length != 2)
                throw new PrefabLoadException("A Vector2 array must contain exactly two elements.");
            return new Vector2(arr[0].GetSingle(), arr[1].GetSingle());
        }

        return new Vector2(el.GetProperty("x").GetSingle(), el.GetProperty("y").GetSingle());
    }
}
