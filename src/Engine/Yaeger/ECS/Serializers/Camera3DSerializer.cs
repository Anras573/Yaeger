using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Camera3D"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "Camera3D",
///   "position": [0.0, 2.0, 5.0],
///   "target": [0.0, 0.0, 0.0],
///   "up": [0.0, 1.0, 0.0],
///   "fov": 0.785,
///   "near": 0.1,
///   "far": 1000.0
/// }
/// </code>
/// <c>fov</c> is in radians. All properties are optional and default to
/// <see cref="Camera3D.Default"/> when absent.
/// </remarks>
public sealed class Camera3DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Camera3D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Camera3D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = Camera3D.Default;
        var position = ComponentJson.GetOptionalVector3(element, "position", defaults.Position);
        var target = ComponentJson.GetOptionalVector3(element, "target", defaults.Target);
        var up = ComponentJson.GetOptionalVector3(element, "up", defaults.Up);
        var fov = ComponentJson.GetOptionalSingle(element, "fov", defaults.Fov);
        var near = ComponentJson.GetOptionalSingle(element, "near", defaults.Near);
        var far = ComponentJson.GetOptionalSingle(element, "far", defaults.Far);

        var component = new Camera3D(position, target, up, fov, near, far);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Camera3D>(entity, out var camera))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["position"] = ComponentJson.Write(camera.Position),
            ["target"] = ComponentJson.Write(camera.Target),
            ["up"] = ComponentJson.Write(camera.Up),
            ["fov"] = camera.Fov,
            ["near"] = camera.Near,
            ["far"] = camera.Far,
        };
    }
}
