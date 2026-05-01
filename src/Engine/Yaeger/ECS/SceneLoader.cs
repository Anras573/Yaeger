using System.Text.Json;

namespace Yaeger.ECS;

/// <summary>
/// Loads <see cref="Scene"/> instances from JSON files or JSON strings by looking up each
/// component's <c>"type"</c> field in a <see cref="ComponentRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// Scene JSON format:
/// <code>
/// {
///   "entities": [
///     {
///       "tag": "player",
///       "components": [
///         { "type": "Sprite", "texturePath": "Assets/player.png" },
///         { "type": "Transform2D", "position": [0.0, 0.0], "scale": [0.1, 0.1] }
///       ]
///     },
///     {
///       "components": [
///         { "type": "Sprite", "texturePath": "Assets/ground.png" }
///       ]
///     }
///   ]
/// }
/// </code>
/// </para>
/// <para>
/// The <c>tag</c> field is optional. Entities without a tag are anonymous; their identity
/// is not preserved across save/load. Scene serializers live in the same
/// <see cref="ComponentRegistry"/> that <see cref="PrefabLoader"/> uses, so engine-provided
/// components (registered via <c>RegisterEngineComponents()</c>) work for both without
/// extra setup.
/// </para>
/// </remarks>
public sealed class SceneLoader
{
    private readonly ComponentRegistry _registry;

    public SceneLoader(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// Loads a <see cref="Scene"/> from a JSON file on disk.
    /// </summary>
    public Scene Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        // Resolve against AppContext.BaseDirectory so the path works regardless of the
        // working directory — same convention used by Texture and FontManager. Without
        // this, `dotnet run` from a parent directory loses the CopyToOutputDirectory path.
        var resolved = AssetPath.Resolve(path);

        if (!File.Exists(resolved))
            throw new FileNotFoundException($"Scene file not found: {path}", resolved);

        var json = File.ReadAllText(resolved);
        return Parse(json);
    }

    /// <summary>
    /// Parses a <see cref="Scene"/> from a JSON string.
    /// </summary>
    public Scene Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new SceneLoadException("Scene JSON must be a non-empty string.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new SceneLoadException("Failed to parse scene JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                throw new SceneLoadException("Scene JSON root must be a JSON object.");

            if (!root.TryGetProperty("entities", out var entitiesEl))
                throw new SceneLoadException(
                    "Scene JSON is missing the required 'entities' array."
                );

            if (entitiesEl.ValueKind != JsonValueKind.Array)
                throw new SceneLoadException("'entities' must be a JSON array.");

            var entries = new List<Scene.SceneEntityEntry>();
            var entityIndex = 0;

            foreach (var entityEl in entitiesEl.EnumerateArray())
            {
                entries.Add(ParseEntity(entityEl, entityIndex));
                entityIndex++;
            }

            return new Scene(entries);
        }
    }

    private Scene.SceneEntityEntry ParseEntity(JsonElement entityEl, int index)
    {
        if (entityEl.ValueKind != JsonValueKind.Object)
            throw new SceneLoadException(
                $"Entity at index {index}: each 'entities' entry must be a JSON object."
            );

        string? tag = null;
        if (entityEl.TryGetProperty("tag", out var tagEl))
        {
            if (tagEl.ValueKind != JsonValueKind.String)
                throw new SceneLoadException(
                    $"Entity at index {index}: 'tag' must be a string when present."
                );

            var rawTag = tagEl.GetString();
            if (!string.IsNullOrWhiteSpace(rawTag))
                tag = rawTag;
        }

        if (!entityEl.TryGetProperty("components", out var componentsEl))
            throw new SceneLoadException(
                $"Entity at index {index}: missing required 'components' array."
            );

        if (componentsEl.ValueKind != JsonValueKind.Array)
            throw new SceneLoadException(
                $"Entity at index {index}: 'components' must be a JSON array."
            );

        var adders = new List<Action<World, Entity>>();
        var componentIndex = 0;

        foreach (var componentEl in componentsEl.EnumerateArray())
        {
            var context = $"Entity {index}, component {componentIndex}: ";
            var adder = _registry.ParseComponent(
                componentEl,
                msg => new SceneLoadException(msg),
                (msg, inner) => new SceneLoadException(msg, inner),
                context
            );
            adders.Add(adder);
            componentIndex++;
        }

        return new Scene.SceneEntityEntry(tag, adders);
    }
}
