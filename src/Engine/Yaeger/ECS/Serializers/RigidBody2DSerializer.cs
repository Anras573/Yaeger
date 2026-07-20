using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Physics.Components;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="RigidBody2D"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "RigidBody2D",
///   "bodyType": "Dynamic",
///   "mass": 1.0,
///   "gravityScale": 1.0,
///   "linearDrag": 0.0
/// }
/// </code>
/// <c>bodyType</c> is required and must be <c>"Dynamic"</c>, <c>"Static"</c>, or
/// <c>"Kinematic"</c> (matching <see cref="BodyType"/>). <c>mass</c> is required (and must be
/// greater than zero) when <c>bodyType</c> is <c>"Dynamic"</c>, and ignored otherwise — static
/// and kinematic bodies always have zero mass. <c>gravityScale</c> (default <c>1.0</c>) and
/// <c>linearDrag</c> (default <c>0.0</c>) are likewise only meaningful for dynamic bodies. This
/// mirrors <see cref="RigidBody2D.CreateDynamic"/>, <see cref="RigidBody2D.CreateStatic"/>, and
/// <see cref="RigidBody2D.CreateKinematic"/> — the only supported ways to construct a valid body.
/// </remarks>
public sealed class RigidBody2DSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "RigidBody2D";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(RigidBody2D);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var bodyType = ReadBodyType(element);

        RigidBody2D component;
        try
        {
            component = bodyType switch
            {
                BodyType.Dynamic => RigidBody2D.CreateDynamic(
                    ReadRequiredMass(element),
                    ComponentJson2D.ReadOptionalSingle(element, "gravityScale", 1f),
                    ComponentJson2D.ReadOptionalSingle(element, "linearDrag", 0f)
                ),
                BodyType.Static => RigidBody2D.CreateStatic(),
                BodyType.Kinematic => RigidBody2D.CreateKinematic(),
                _ => throw new PrefabLoadException(
                    $"RigidBody2D has unrecognized 'bodyType' value '{bodyType}'."
                ),
            };
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new PrefabLoadException(
                $"RigidBody2D has invalid property values: {ex.Message}",
                ex
            );
        }

        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<RigidBody2D>(entity, out var body))
            return null;

        var obj = new JsonObject { ["type"] = TypeId, ["bodyType"] = body.Type.ToString() };

        if (body.Type == BodyType.Dynamic)
        {
            obj["mass"] = body.Mass;
            obj["gravityScale"] = body.GravityScale;
            obj["linearDrag"] = body.LinearDrag;
        }

        return obj;
    }

    private static BodyType ReadBodyType(JsonElement element)
    {
        if (!element.TryGetProperty("bodyType", out var bodyTypeEl))
            throw new PrefabLoadException("RigidBody2D is missing required 'bodyType' property.");

        if (
            bodyTypeEl.ValueKind != JsonValueKind.String
            || !Enum.TryParse<BodyType>(bodyTypeEl.GetString(), out var bodyType)
            || !Enum.IsDefined(bodyType)
        )
            throw new PrefabLoadException(
                "Property 'bodyType' must be one of \"Dynamic\", \"Static\", or \"Kinematic\"."
            );

        return bodyType;
    }

    private static float ReadRequiredMass(JsonElement element)
    {
        if (!element.TryGetProperty("mass", out var massEl))
            throw new PrefabLoadException(
                "RigidBody2D with bodyType 'Dynamic' is missing required 'mass' property."
            );

        return ComponentJson2D.ReadSingle(massEl, "mass");
    }
}
