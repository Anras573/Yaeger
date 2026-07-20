using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Physics.Components;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="PhysicsMaterial"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "PhysicsMaterial",
///   "restitution": 0.3,
///   "friction": 0.4
/// }
/// </code>
/// Both properties are optional and default to <see cref="PhysicsMaterial.Default"/>
/// (restitution 0.3, friction 0.4). <c>restitution</c> must be in <c>[0, 1]</c> and
/// <c>friction</c> must be non-negative.
/// </remarks>
public sealed class PhysicsMaterialSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "PhysicsMaterial";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(PhysicsMaterial);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = PhysicsMaterial.Default;
        var restitution = ComponentJson2D.ReadOptionalSingle(
            element,
            "restitution",
            defaults.Restitution
        );
        var friction = ComponentJson2D.ReadOptionalSingle(element, "friction", defaults.Friction);

        PhysicsMaterial component;
        try
        {
            component = new PhysicsMaterial(restitution, friction);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new PrefabLoadException(
                $"PhysicsMaterial has invalid property values: {ex.Message}",
                ex
            );
        }

        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<PhysicsMaterial>(entity, out var material))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["restitution"] = material.Restitution,
            ["friction"] = material.Friction,
        };
    }
}
