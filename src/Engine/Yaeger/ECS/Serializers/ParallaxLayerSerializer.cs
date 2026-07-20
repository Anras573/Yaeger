using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="ParallaxLayer"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "ParallaxLayer",
///   "scrollFactorX": 0.5,
///   "scrollFactorY": 0.0,
///   "basePosition": [0.0, 0.0]
/// }
/// </code>
/// All properties are optional and default to <see cref="ParallaxLayer"/>'s parameterless
/// constructor values (<c>scrollFactorX</c> 0.5, <c>scrollFactorY</c> 0, <c>basePosition</c> zero).
/// </remarks>
public sealed class ParallaxLayerSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "ParallaxLayer";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(ParallaxLayer);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var defaults = new ParallaxLayer();

        var component = new ParallaxLayer(
            ComponentJson2D.ReadOptionalSingle(element, "scrollFactorX", defaults.ScrollFactorX),
            ComponentJson2D.ReadOptionalSingle(element, "scrollFactorY", defaults.ScrollFactorY)
        )
        {
            BasePosition = element.TryGetProperty("basePosition", out var basePosEl)
                ? ComponentJson2D.ReadVector2(basePosEl, "basePosition")
                : defaults.BasePosition,
        };

        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<ParallaxLayer>(entity, out var layer))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["scrollFactorX"] = layer.ScrollFactorX,
            ["scrollFactorY"] = layer.ScrollFactorY,
            ["basePosition"] = ComponentJson2D.Write(layer.BasePosition),
        };
    }
}
