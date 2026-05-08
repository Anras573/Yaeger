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
/// The output format is identical to the format consumed by <see cref="SceneLoader"/>.
/// A save/load round-trip produces an equivalent world for all entities whose components
/// are covered by serializers registered in the <see cref="ComponentRegistry"/>; components
/// whose serializers return <c>null</c> from <see cref="IComponentSerializer.TrySerialize"/>
/// are silently omitted and will not be present after reloading:
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
/// Entity order in the output file is deterministic: entities are written in ascending
/// <see cref="Entity.Id"/> order.  Component order within each entity follows the
/// registration order of the serializers in the <see cref="ComponentRegistry"/>.
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
    /// The path is resolved via <see cref="AssetPath.Resolve"/> (against
    /// <see cref="AppContext.BaseDirectory"/>) so that relative paths behave the same way
    /// as in <see cref="SceneLoader.Load"/>. The write is done via a sibling <c>.tmp</c>
    /// file that is then renamed over the destination so a crash never leaves a partially
    /// written file in place. The parent directory must already exist.
    /// </remarks>
    /// <param name="world">The world whose entities should be saved.</param>
    /// <param name="path">Destination file path.</param>
    /// <exception cref="SceneSaveException">
    /// Thrown when a serializer fails or the file cannot be written.
    /// </exception>
    public void Save(World world, string path)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        var resolved = AssetPath.Resolve(path);
        var json = Serialize(world);
        var tmp = resolved + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, resolved, overwrite: true);
        }
        catch (Exception ex)
        {
            throw new SceneSaveException($"Failed to write scene to '{path}': {ex.Message}", ex);
        }
        finally
        {
            try
            {
                File.Delete(tmp);
            }
            catch
            { /* best-effort cleanup — don't mask the original exception */
            }
        }
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
        var serializers = _registry.Serializers;

        foreach (var entity in world.Entities.OrderBy(e => e.Id))
        {
            var entityObj = new JsonObject();

            if (world.TryGetTag(entity, out var tag))
                entityObj["tag"] = tag;

            var entityLabel = tag is not null ? $"'{tag}'" : $"id={entity.Id}";
            var components = new JsonArray();
            foreach (var serializer in serializers)
            {
                JsonNode? node;
                try
                {
                    node = serializer.TrySerialize(world, entity);
                }
                catch (Exception ex)
                {
                    throw new SceneSaveException(
                        $"Serializer '{serializer.TypeId}' failed on entity {entityLabel}.",
                        ex
                    );
                }

                if (node is null)
                    continue;

                if (
                    node is not JsonObject obj
                    || obj["type"] is not JsonValue typeNode
                    || !typeNode.TryGetValue<string>(out var typeStr)
                    || string.IsNullOrWhiteSpace(typeStr)
                )
                {
                    throw new SceneSaveException(
                        $"Serializer '{serializer.TypeId}' returned a node for entity {entityLabel} "
                            + "that is not a JSON object with a non-empty 'type' field. "
                            + "Custom TrySerialize implementations must include a 'type' field."
                    );
                }

                if (typeStr != serializer.TypeId)
                {
                    throw new SceneSaveException(
                        $"Serializer '{serializer.TypeId}' returned a node for entity {entityLabel} "
                            + $"with a mismatched 'type' field ('{typeStr}'). "
                            + "The 'type' field must match the serializer's TypeId."
                    );
                }

                components.Add(node);
            }

            entityObj["components"] = components;
            entities.Add(entityObj);
        }

        var root = new JsonObject { ["entities"] = entities };
        return root.ToJsonString(IndentedOptions);
    }
}
