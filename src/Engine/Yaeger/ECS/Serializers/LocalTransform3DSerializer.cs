using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="LocalTransform3D"/> component.
/// </summary>
/// <remarks>
/// Same shape as <see cref="Transform3DSerializer"/>:
/// <code>
/// {
///   "type": "LocalTransform3D",
///   "position": [0.0, 0.0, 0.0],
///   "rotation": [0.0, 0.0, 0.0, 1.0],
///   "scale": [1.0, 1.0, 1.0]
/// }
/// </code>
/// <c>rotation</c> is a quaternion <c>[x, y, z, w]</c>. All properties are optional and default to
/// the identity transform (zero position, identity rotation, unit scale) when absent.
/// </remarks>
public sealed class LocalTransform3DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "LocalTransform3D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(LocalTransform3D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var position = ComponentJson.GetOptionalVector3(element, "position", Vector3.Zero);
        var rotation = ComponentJson.GetOptionalQuaternion(
            element,
            "rotation",
            Quaternion.Identity
        );
        var scale = ComponentJson.GetOptionalVector3(element, "scale", Vector3.One);

        var component = new LocalTransform3D(position, rotation, scale);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<LocalTransform3D>(entity, out var t))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["position"] = ComponentJson.Write(t.Position),
            ["rotation"] = ComponentJson.Write(t.Rotation),
            ["scale"] = ComponentJson.Write(t.Scale),
        };
    }
}
