using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Physics.Components;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="CircleCollider2D"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "CircleCollider2D",
///   "radius": 0.5,
///   "offset": [0.0, 0.0],
///   "layer": 0,
///   "collidesWith": 4294967295,
///   "isTrigger": false
/// }
/// </code>
/// <c>radius</c> is required and must be greater than zero. <c>offset</c> defaults to zero,
/// <c>layer</c> defaults to <c>0</c>, <c>collidesWith</c> defaults to
/// <see cref="CircleCollider2D.AllLayers"/> (collides with everything), and <c>isTrigger</c>
/// defaults to <c>false</c>.
/// </remarks>
public sealed class CircleCollider2DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "CircleCollider2D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(CircleCollider2D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        if (!element.TryGetProperty("radius", out var radiusEl))
            throw new PrefabLoadException(
                "CircleCollider2D is missing required 'radius' property."
            );

        var radius = ComponentJson2D.ReadSingle(radiusEl, "radius");
        var offset = element.TryGetProperty("offset", out var offsetEl)
            ? ComponentJson2D.ReadVector2(offsetEl, "offset")
            : Vector2.Zero;

        var (layer, collidesWith, isTrigger) = ComponentJson2D.ReadCollisionFiltering(element);

        CircleCollider2D component;
        try
        {
            component = new CircleCollider2D(radius, offset, layer, collidesWith, isTrigger);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new PrefabLoadException(
                $"CircleCollider2D has invalid property values: {ex.Message}",
                ex
            );
        }

        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<CircleCollider2D>(entity, out var collider))
            return null;

        var obj = new JsonObject { ["type"] = TypeId, ["radius"] = collider.Radius };

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
