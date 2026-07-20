using System.Text.Json;
using System.Text.Json.Nodes;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Parent"/> component.
/// </summary>
/// <remarks>
/// <para>
/// JSON format:
/// <code>
/// { "type": "Parent", "parentTag": "tank" }
/// </code>
/// <c>parentTag</c> (required) is the tag of the parent entity, resolved via
/// <see cref="World.GetEntity(string)"/> when this component is applied to a world. Tags are the
/// only cross-entity reference the prefab/scene format supports.
/// </para>
/// <para>
/// Because resolution happens when the component is applied (not when the JSON is parsed), a
/// <c>Parent</c> inside a scene file may reference a tag defined anywhere else in the same scene
/// — <see cref="Scene.Apply"/> creates every entity (and registers its tag) before applying any
/// component. Inside a prefab, <c>parentTag</c> must already be registered on the target
/// <see cref="World"/> before <see cref="World.Instantiate(Prefab, string?)"/> is called, since a
/// prefab only ever describes a single entity.
/// </para>
/// </remarks>
public sealed class ParentSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Parent";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Parent);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        if (
            !element.TryGetProperty("parentTag", out var tagEl)
            || tagEl.ValueKind != JsonValueKind.String
        )
            throw new PrefabLoadException("Property 'parentTag' must be a string.");

        var parentTag = tagEl.GetString();
        if (string.IsNullOrWhiteSpace(parentTag))
            throw new PrefabLoadException("Property 'parentTag' must be a non-empty string.");

        return (world, entity) =>
        {
            if (!world.TryGetEntity(parentTag, out var parentEntity))
                throw new PrefabLoadException(
                    $"Parent component on entity {entity.Id} references unknown tag '{parentTag}'. "
                        + "The parent entity must already be tagged with that value when this "
                        + "component is applied."
                );

            world.AddComponent(entity, new Parent(parentEntity));
        };
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Parent>(entity, out var parent))
            return null;

        if (!world.TryGetTag(parent.ParentEntity, out var parentTag))
            throw new InvalidOperationException(
                $"Entity {entity.Id} has a Parent referencing untagged entity "
                    + $"{parent.ParentEntity.Id}. Scenes can only express Parent references via "
                    + "tags — tag the parent entity before saving."
            );

        return new JsonObject { ["type"] = TypeId, ["parentTag"] = parentTag };
    }
}
