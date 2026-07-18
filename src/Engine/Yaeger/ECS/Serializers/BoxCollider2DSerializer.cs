using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Physics.Components;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="BoxCollider2D"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "BoxCollider2D",
///   "size": [1.0, 1.0],
///   "offset": [0.0, 0.0],
///   "layer": 0,
///   "collidesWith": 4294967295,
///   "isTrigger": false
/// }
/// </code>
/// <c>size</c> is required (a two-element <c>[width, height]</c> array or
/// <c>{ "x": number, "y": number }</c> object; both components must be greater than zero).
/// <c>offset</c> defaults to zero, <c>layer</c> defaults to <c>0</c>, <c>collidesWith</c>
/// defaults to <see cref="BoxCollider2D.AllLayers"/> (collides with everything), and
/// <c>isTrigger</c> defaults to <c>false</c>.
/// </remarks>
public sealed class BoxCollider2DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "BoxCollider2D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(BoxCollider2D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        if (!element.TryGetProperty("size", out var sizeEl))
            throw new PrefabLoadException("BoxCollider2D is missing required 'size' property.");

        var size = ComponentJson2D.ReadVector2(sizeEl, "size");
        var offset = element.TryGetProperty("offset", out var offsetEl)
            ? ComponentJson2D.ReadVector2(offsetEl, "offset")
            : Vector2.Zero;

        var (layer, collidesWith, isTrigger) = ComponentJson2D.ReadCollisionFiltering(element);

        BoxCollider2D component;
        try
        {
            component = new BoxCollider2D(size, offset, layer, collidesWith, isTrigger);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new PrefabLoadException(
                $"BoxCollider2D has invalid property values: {ex.Message}",
                ex
            );
        }

        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<BoxCollider2D>(entity, out var collider))
            return null;

        var obj = new JsonObject
        {
            ["type"] = TypeId,
            ["size"] = ComponentJson2D.Write(collider.Size),
        };

        if (collider.Offset != Vector2.Zero)
            obj["offset"] = ComponentJson2D.Write(collider.Offset);

        ComponentJson2D.WriteCollisionFiltering(
            obj,
            collider.Layer,
            collider.CollidesWith,
            collider.IsTrigger
        );

        return obj;
    }
}
