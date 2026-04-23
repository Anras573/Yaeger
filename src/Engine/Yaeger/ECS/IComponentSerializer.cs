using System.Text.Json;

namespace Yaeger.ECS;

/// <summary>
/// Defines a serializer for a single component type, enabling JSON-backed prefabs.
/// </summary>
/// <remarks>
/// Implement this interface and register the implementation with a
/// <see cref="ComponentRegistry"/> so that <see cref="PrefabLoader"/> can recognise
/// and deserialize your component type from JSON prefab files.
/// </remarks>
public interface IComponentSerializer
{
    /// <summary>
    /// The stable string identifier for this component type as it appears in prefab JSON files.
    /// </summary>
    /// <remarks>
    /// This value is matched against the <c>"type"</c> field of each component entry in a
    /// prefab file.  Choose a short, human-readable name (e.g. <c>"Sprite"</c>,
    /// <c>"Transform2D"</c>).  Changing this value breaks existing prefab files.
    /// </remarks>
    string TypeId { get; }

    /// <summary>
    /// Deserializes a component from a <see cref="JsonElement"/> and returns an action
    /// that adds the resulting component to an entity.
    /// </summary>
    /// <param name="element">The JSON element representing the component object.</param>
    /// <returns>
    /// An action that, when invoked with a <see cref="World"/> and an <see cref="Entity"/>,
    /// adds the deserialized component to that entity.
    /// </returns>
    Action<World, Entity> Deserialize(JsonElement element);
}
