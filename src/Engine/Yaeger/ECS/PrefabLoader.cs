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
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        // Resolve against AppContext.BaseDirectory so the path works regardless of the
        // working directory — matches Texture / FontManager / SceneLoader.
        var resolved = AssetPath.Resolve(path);

        if (!File.Exists(resolved))
            throw new FileNotFoundException($"Prefab file not found: {path}", resolved);

        var json = File.ReadAllText(resolved);
        return Parse(json);
    }

    /// <summary>
    /// Loads a <see cref="Prefab"/> from a URL over HTTP.
    /// Works in desktop and WASM environments (the WASM HttpClient routes through the
    /// browser Fetch API, making this call async-safe with no blocking I/O).
    /// </summary>
    /// <param name="url">Absolute HTTP/HTTPS URL of the <c>.prefab.json</c> resource.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for the request.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="PrefabLoadException">
    /// When the request fails, returns a non-success HTTP status, or the JSON is invalid.
    /// </exception>
    public async Task<Prefab> LoadAsync(
        string url,
        HttpClient httpClient,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));
        ArgumentNullException.ThrowIfNull(httpClient);

        try
        {
            using var response = await httpClient
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new PrefabLoadException(
                    $"HTTP {(int)response.StatusCode} fetching prefab from '{url}'."
                );

            var json = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            return Parse(json);
        }
        catch (HttpRequestException ex)
        {
            throw new PrefabLoadException(
                $"Failed to fetch prefab from '{url}': {ex.Message}",
                ex
            );
        }
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
        if (string.IsNullOrWhiteSpace(json))
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
                var adder = _registry.ParseComponent(
                    componentEl,
                    msg => new PrefabLoadException(msg),
                    (msg, inner) => new PrefabLoadException(msg, inner)
                );
                builder.WithAction(adder);
            }

            return builder.Build();
        }
    }
}
