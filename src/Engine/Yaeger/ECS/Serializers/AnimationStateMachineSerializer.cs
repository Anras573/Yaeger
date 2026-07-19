using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="AnimationStateMachine"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "AnimationStateMachine",
///   "currentState": "idle",
///   "restartOnReplay": false,
///   "states": {
///     "idle": { "loop": true, "frames": [{ "texturePath": "Assets/idle0.png", "duration": 0.2 }] },
///     "jump": { "loop": false, "frames": [{ "texturePath": "Assets/jump0.png", "duration": 0.15 }] }
///   }
/// }
/// </code>
/// Each entry in <c>states</c> uses the same <c>loop</c>/<c>frames</c> shape as a standalone
/// <see cref="Animation"/> component (see <see cref="AnimationSerializer"/>). <c>restartOnReplay</c>
/// is optional and defaults to <c>false</c>. <c>RequestedState</c> is transient system state, not
/// persisted.
/// </remarks>
public sealed class AnimationStateMachineSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "AnimationStateMachine";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(AnimationStateMachine);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        if (!element.TryGetProperty("states", out var statesEl))
            throw new PrefabLoadException(
                "AnimationStateMachine is missing required 'states' property."
            );

        if (statesEl.ValueKind != JsonValueKind.Object)
            throw new PrefabLoadException("AnimationStateMachine 'states' must be a JSON object.");

        var states = new Dictionary<string, Animation>();
        foreach (var stateProperty in statesEl.EnumerateObject())
        {
            states[stateProperty.Name] = AnimationSerializer.ParseAnimationBody(
                stateProperty.Value
            );
        }

        if (states.Count == 0)
            throw new PrefabLoadException(
                "AnimationStateMachine 'states' must contain at least one entry."
            );

        if (!element.TryGetProperty("currentState", out var currentStateEl))
            throw new PrefabLoadException(
                "AnimationStateMachine is missing required 'currentState' property."
            );

        if (currentStateEl.ValueKind != JsonValueKind.String)
            throw new PrefabLoadException("AnimationStateMachine 'currentState' must be a string.");

        var currentState = currentStateEl.GetString();
        if (string.IsNullOrWhiteSpace(currentState))
            throw new PrefabLoadException(
                "AnimationStateMachine 'currentState' must be a non-empty string."
            );

        if (!states.ContainsKey(currentState))
            throw new PrefabLoadException(
                $"AnimationStateMachine 'currentState' ('{currentState}') is not one of the defined 'states'."
            );

        var restartOnReplay = ComponentJson2D.ReadOptionalBool(element, "restartOnReplay", false);

        var component = new AnimationStateMachine(states, currentState, restartOnReplay);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<AnimationStateMachine>(entity, out var machine))
            return null;

        var states = new JsonObject();
        foreach (var (name, clip) in machine.States)
        {
            states[name] = AnimationSerializer.WriteAnimationBody(clip);
        }

        var json = new JsonObject
        {
            ["type"] = TypeId,
            ["currentState"] = machine.CurrentState,
            ["states"] = states,
        };

        if (machine.RestartOnReplay)
            json["restartOnReplay"] = true;

        return json;
    }
}
