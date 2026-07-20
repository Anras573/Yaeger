using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="ParticleEmitter"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "ParticleEmitter",
///   "texturePath": "Assets/particle.png",
///   "maxParticles": 256,
///   "emitRate": 50.0,
///   "particleLifetime": 1.0,
///   "emitDirection": [0.0, 1.0],
///   "spreadAngle": 0.7853982,
///   "initialSpeed": 1.0,
///   "startColor": [255, 255, 255, 255],
///   "endColor": [255, 255, 255, 255],
///   "startSize": 0.1,
///   "endSize": 0.1
/// }
/// </code>
/// <c>texturePath</c> is required (matching <see cref="Sprite"/>'s convention); every other
/// property is optional and defaults to the value <see cref="ParticleEmitter"/>'s primary
/// constructor assigns.
/// </remarks>
public sealed class ParticleEmitterSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "ParticleEmitter";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(ParticleEmitter);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var texturePath = GetRequiredTexturePath(element);
        var defaults = new ParticleEmitter(texturePath);

        var component = new ParticleEmitter(texturePath)
        {
            MaxParticles = ReadOptionalInt(element, "maxParticles", defaults.MaxParticles),
            EmitRate = ComponentJson2D.ReadOptionalSingle(element, "emitRate", defaults.EmitRate),
            ParticleLifetime = ComponentJson2D.ReadOptionalSingle(
                element,
                "particleLifetime",
                defaults.ParticleLifetime
            ),
            EmitDirection = element.TryGetProperty("emitDirection", out var dirEl)
                ? ComponentJson2D.ReadVector2(dirEl, "emitDirection")
                : defaults.EmitDirection,
            SpreadAngle = ComponentJson2D.ReadOptionalSingle(
                element,
                "spreadAngle",
                defaults.SpreadAngle
            ),
            InitialSpeed = ComponentJson2D.ReadOptionalSingle(
                element,
                "initialSpeed",
                defaults.InitialSpeed
            ),
            StartColor = ComponentJson.GetOptionalColor(element, "startColor", defaults.StartColor),
            EndColor = ComponentJson.GetOptionalColor(element, "endColor", defaults.EndColor),
            StartSize = ComponentJson2D.ReadOptionalSingle(
                element,
                "startSize",
                defaults.StartSize
            ),
            EndSize = ComponentJson2D.ReadOptionalSingle(element, "endSize", defaults.EndSize),
        };

        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<ParticleEmitter>(entity, out var emitter))
            return null;

        return new JsonObject
        {
            ["type"] = TypeId,
            ["texturePath"] = emitter.TexturePath,
            ["maxParticles"] = emitter.MaxParticles,
            ["emitRate"] = emitter.EmitRate,
            ["particleLifetime"] = emitter.ParticleLifetime,
            ["emitDirection"] = ComponentJson2D.Write(emitter.EmitDirection),
            ["spreadAngle"] = emitter.SpreadAngle,
            ["initialSpeed"] = emitter.InitialSpeed,
            ["startColor"] = ComponentJson.Write(emitter.StartColor),
            ["endColor"] = ComponentJson.Write(emitter.EndColor),
            ["startSize"] = emitter.StartSize,
            ["endSize"] = emitter.EndSize,
        };
    }

    private static string GetRequiredTexturePath(JsonElement element)
    {
        if (!element.TryGetProperty("texturePath", out var texturePathEl))
            throw new PrefabLoadException(
                "ParticleEmitter component is missing required 'texturePath' property."
            );

        if (texturePathEl.ValueKind != JsonValueKind.String)
            throw new PrefabLoadException("ParticleEmitter 'texturePath' must be a string.");

        var texturePath = texturePathEl.GetString();
        if (string.IsNullOrWhiteSpace(texturePath))
            throw new PrefabLoadException(
                "ParticleEmitter 'texturePath' must be a non-empty string."
            );

        return texturePath;
    }

    private static int ReadOptionalInt(JsonElement element, string propertyName, int defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var el))
            return defaultValue;

        if (!el.TryGetInt32(out var value))
            throw new PrefabLoadException($"ParticleEmitter '{propertyName}' must be an integer.");

        return value;
    }
}
