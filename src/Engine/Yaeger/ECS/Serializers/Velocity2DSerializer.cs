using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Physics.Components;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Velocity2D"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "Velocity2D",
///   "linear": [0.0, 0.0],
///   "angular": 0.0
/// }
/// </code>
/// Both properties are optional and default to zero, matching <see cref="Velocity2D.Zero"/>.
/// </remarks>
public sealed class Velocity2DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Velocity2D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Velocity2D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var linear = element.TryGetProperty("linear", out var linearEl)
            ? ComponentJson2D.ReadVector2(linearEl, "linear")
            : Vector2.Zero;

        var angular = ComponentJson2D.ReadOptionalSingle(element, "angular", 0f);

        var component = new Velocity2D(linear, angular);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Velocity2D>(entity, out var velocity))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["linear"] = ComponentJson2D.Write(velocity.Linear),
            ["angular"] = velocity.Angular,
        };
    }
}
