using System.Text.Json;
using System.Text.Json.Nodes;

namespace Yaeger.ECS;

/// <summary>
/// Serializes a <see cref="World"/> to a JSON scene file by asking each registered
/// <see cref="IComponentSerializer"/> to write its component type for every entity.
/// </summary>
/// <remarks>
/// <para>
/// Only component types whose <see cref="IComponentSerializer"/> implements
/// <see cref="IComponentSerializer.TrySerialize"/> (i.e. returns a non-<c>null</c>
/// <see cref="JsonNode"/>) will appear in the saved file.  The engine-provided serializers
/// all support the write direction; custom serializers may opt in by overriding the default
/// <c>TrySerialize</c> method.
/// </para>
/// <para>
/// The output format is identical to the format consumed by <see cref="SceneLoader"/>, so a
/// save/load round-trip always produces an equivalent world:
/// <code>
/// var saver = new SceneSaver(registry);
/// saver.Save(world, "Scenes/level1.json");
///
/// var loader = new SceneLoader(registry);
/// var scene = loader.Load("Scenes/level1.json");
/// world.Instantiate(scene);
/// </code>
/// </para>
/// <para>
/// Entity order in the output file follows the iteration order of
/// <see cref="World.Entities"/>, which is insertion order (entities created first appear
/// first).  Component order within each entity follows the registration order of the
/// serializers in the <see cref="ComponentRegistry"/>.
/// </para>
/// </remarks>
public sealed class SceneSaver
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    private readonly ComponentRegistry _registry;

    /// <summary>
    /// Initializes a new <see cref="SceneSaver"/> that uses <paramref name="registry"/> to
    /// find the serializer for each component type.
    /// </summary>
    public SceneSaver(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// Serializes <paramref name="world"/> to a JSON scene file at <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// The path is used as-is (relative paths resolve against the process working directory).
    /// The parent directory must already exist.
    /// </remarks>
    /// <param name="world">The world whose entities should be saved.</param>
    /// <param name="path">Destination file path.</param>
    public void Save(World world, string path)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        var json = Serialize(world);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Serializes <paramref name="world"/> to a JSON string without writing to disk.
    /// Useful for testing or in-memory round-trips.
    /// </summary>
    /// <param name="world">The world whose entities should be serialized.</param>
    /// <returns>An indented JSON string in scene-file format.</returns>
    public string Serialize(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var entities = new JsonArray();

        foreach (var entity in world.Entities)
        {
            var entityObj = new JsonObject();

            if (world.TryGetTag(entity, out var tag))
                entityObj["tag"] = tag;

            var components = new JsonArray();
            foreach (var serializer in _registry.Serializers)
            {
                var node = serializer.TrySerialize(world, entity);
                if (node is not null)
                    components.Add(node);
            }

            entityObj["components"] = components;
            entities.Add(entityObj);
        }

        var root = new JsonObject { ["entities"] = entities };
        return root.ToJsonString(IndentedOptions);
    }
}
