using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="LocalTransform2D"/> component.
/// </summary>
/// <remarks>
/// Same shape as <see cref="Transform2DSerializer"/> — Vector2 values can be specified as a
/// two-element array <c>[x, y]</c> or as an object <c>{ "x": number, "y": number }</c>.
/// <code>
/// {
///   "type": "LocalTransform2D",
///   "position": [0.0, 0.0],
///   "rotation": 0.0,
///   "scale": { "x": 1.0, "y": 1.0 }
/// }
/// </code>
/// All properties are optional and default to their zero/identity values when absent.
/// </remarks>
public sealed class LocalTransform2DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "LocalTransform2D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(LocalTransform2D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var position = element.TryGetProperty("position", out var posEl)
            ? ComponentJson2D.ReadVector2(posEl, "position")
            : Vector2.Zero;

        var rotation = ComponentJson2D.ReadOptionalSingle(element, "rotation", 0f);

        var scale = element.TryGetProperty("scale", out var scaleEl)
            ? ComponentJson2D.ReadVector2(scaleEl, "scale")
            : Vector2.One;

        var component = new LocalTransform2D(position, rotation, scale);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<LocalTransform2D>(entity, out var t))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["position"] = ComponentJson2D.Write(t.Position),
            ["rotation"] = t.Rotation,
            ["scale"] = ComponentJson2D.Write(t.Scale),
        };
    }
}
