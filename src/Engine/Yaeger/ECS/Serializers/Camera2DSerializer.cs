using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Camera2D"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "Camera2D",
///   "position": [0.0, 0.0],
///   "zoom": 1.0,
///   "rotation": 0.0
/// }
/// </code>
/// All properties are optional and default to <see cref="Camera2D"/>'s parameterless
/// constructor values (position zero, zoom 1, rotation 0).
/// </remarks>
public sealed class Camera2DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Camera2D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Camera2D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var position = element.TryGetProperty("position", out var posEl)
            ? ComponentJson2D.ReadVector2(posEl, "position")
            : Vector2.Zero;

        var zoom = ComponentJson2D.ReadOptionalSingle(element, "zoom", 1f);
        var rotation = ComponentJson2D.ReadOptionalSingle(element, "rotation", 0f);

        var component = new Camera2D(position, zoom, rotation);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Camera2D>(entity, out var camera))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["position"] = ComponentJson2D.Write(camera.Position),
            ["zoom"] = camera.Zoom,
            ["rotation"] = camera.Rotation,
        };
    }
}
