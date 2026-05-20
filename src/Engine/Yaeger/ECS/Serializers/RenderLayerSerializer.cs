using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="RenderLayer"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// { "type": "RenderLayer", "value": 10 }
/// </code>
/// The value defaults to 0 when omitted.
/// </remarks>
public sealed class RenderLayerSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "RenderLayer";

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var value = 0;
        if (element.TryGetProperty("value", out var valueEl) && !valueEl.TryGetInt32(out value))
        {
            throw new PrefabLoadException("RenderLayer 'value' must be an integer.");
        }

        var component = new RenderLayer(value);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<RenderLayer>(entity, out var renderLayer))
            return null;

        return new JsonObject { ["type"] = TypeId, ["value"] = renderLayer.Value };
    }
}
