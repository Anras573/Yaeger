using System.Text.Json;

namespace Yaeger.ECS;

/// <summary>
/// Loads <see cref="Prefab"/> instances from JSON files or JSON strings by looking up
/// each component's <c>"type"</c> field in a <see cref="ComponentRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// Prefab JSON format:
/// <code>
/// {
///   "components": [
///     { "type": "Sprite", "texturePath": "Assets/ball.png" },
///     { "type": "Transform2D", "position": [0.0, 0.0], "rotation": 0.0, "scale": [0.025, 0.025] }
///   ]
/// }
/// </code>
/// </para>
/// <para>
/// Register serializers for each component type with a <see cref="ComponentRegistry"/>
/// before calling <see cref="Load"/> or <see cref="Parse"/>.
/// Engine-provided component serializers can be registered via the
/// <c>RegisterEngineComponents()</c> extension method.
/// </para>
/// </remarks>
public sealed class PrefabLoader
{
    private readonly ComponentRegistry _registry;

    /// <summary>
    /// Initializes a new <see cref="PrefabLoader"/> backed by the given <paramref name="registry"/>.
    /// </summary>
    public PrefabLoader(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// Loads a <see cref="Prefab"/> from a JSON file on disk.
    /// </summary>
    /// <param name="path">Path to the <c>.prefab.json</c> file.</param>
    /// <exception cref="FileNotFoundException">When the file does not exist.</exception>
    /// <exception cref="PrefabLoadException">
    /// When the JSON is malformed, the <c>components</c> array is absent, or a component
    /// type is not registered.
    /// </exception>
    public Prefab Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prefab file not found: {path}", path);

        var json = File.ReadAllText(path);
        return Parse(json);
    }

    /// <summary>
    /// Parses a <see cref="Prefab"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <exception cref="PrefabLoadException">
    /// When the JSON is malformed, the <c>components</c> array is absent, or a component
    /// type is not registered.
    /// </exception>
    public Prefab Parse(string json)
    {
        if (string.IsNullOrEmpty(json))
            throw new PrefabLoadException("Prefab JSON must be a non-empty string.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new PrefabLoadException("Failed to parse prefab JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                throw new PrefabLoadException("Prefab JSON root must be a JSON object.");

            if (!root.TryGetProperty("components", out var componentsEl))
                throw new PrefabLoadException(
                    "Prefab JSON is missing the required 'components' array."
                );

            if (componentsEl.ValueKind != JsonValueKind.Array)
                throw new PrefabLoadException("'components' must be a JSON array.");

            var builder = new PrefabBuilder();

            foreach (var componentEl in componentsEl.EnumerateArray())
            {
                if (componentEl.ValueKind != JsonValueKind.Object)
                    throw new PrefabLoadException(
                        "Each component entry in 'components' must be a JSON object."
                    );

                if (!componentEl.TryGetProperty("type", out var typeEl))
                    throw new PrefabLoadException(
                        "Each component entry must have a 'type' property."
                    );

                if (typeEl.ValueKind != JsonValueKind.String)
                    throw new PrefabLoadException("Component 'type' must be a non-empty string.");

                var typeId = typeEl.GetString();
                if (string.IsNullOrWhiteSpace(typeId))
                    throw new PrefabLoadException("Component 'type' must be a non-empty string.");

                if (!_registry.TryGetSerializer(typeId, out var serializer))
                {
                    var registered = string.Join(", ", _registry.RegisteredTypeIds);
                    throw new PrefabLoadException(
                        $"No serializer is registered for component type '{typeId}'. "
                            + $"Registered types: [{registered}]"
                    );
                }

                Action<World, Entity> adder;
                try
                {
                    adder = serializer.Deserialize(componentEl);
                }
                catch (Exception ex) when (ex is not PrefabLoadException)
                {
                    throw new PrefabLoadException(
                        $"Failed to deserialize component '{typeId}'.",
                        ex
                    );
                }

                builder.WithAction(adder);
            }

            return builder.Build();
        }
    }
}
