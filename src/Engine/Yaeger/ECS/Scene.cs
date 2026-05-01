namespace Yaeger.ECS;

/// <summary>
/// A reusable multi-entity template loaded from a JSON scene file. Each entry carries an
/// optional tag and a list of component-adder actions (mirror of <see cref="Prefab"/>'s
/// internal representation).
/// </summary>
/// <remarks>
/// <para>
/// Construct a <see cref="Scene"/> via <see cref="SceneLoader"/>. Apply it to a
/// <see cref="World"/> with <c>world.Instantiate(scene)</c>, which creates every
/// entity described in the scene file.
/// </para>
/// <para>
/// Scenes are read-only value containers. They can safely be kept alive and applied to
/// multiple worlds, although the <see cref="Action{T1, T2}"/> closures may capture
/// reference-typed component values that would then be shared across worlds — the same
/// caveat as <see cref="Prefab"/>.
/// </para>
/// </remarks>
public sealed class Scene
{
    private readonly IReadOnlyList<SceneEntityEntry> _entities;

    internal Scene(IReadOnlyList<SceneEntityEntry> entities)
    {
        _entities = entities;
    }

    /// <summary>Number of entities described by this scene.</summary>
    public int EntityCount => _entities.Count;

    /// <summary>
    /// Spawns every entity in the scene into <paramref name="world"/>, restoring tags and
    /// applying each entity's components.
    /// </summary>
    /// <returns>The created entities in scene-file order.</returns>
    /// <exception cref="ArgumentException">
    /// When a scene entity's tag collides with a tag already present in the world.
    /// Prefer catching this and reporting which scene file or entity was at fault; the
    /// scene loader can't prevent collisions with entities the user created in code.
    /// </exception>
    internal IReadOnlyList<Entity> Apply(World world)
    {
        var created = new List<Entity>(_entities.Count);
        foreach (var entry in _entities)
        {
            var entity = string.IsNullOrWhiteSpace(entry.Tag)
                ? world.CreateEntity()
                : world.CreateEntity(entry.Tag);

            foreach (var adder in entry.ComponentAdders)
            {
                adder(world, entity);
            }

            created.Add(entity);
        }
        return created.AsReadOnly();
    }

    /// <summary>
    /// One scene entity: an optional tag plus the component adders produced by
    /// <see cref="IComponentSerializer.Deserialize"/>.
    /// </summary>
    internal readonly record struct SceneEntityEntry(
        string? Tag,
        IReadOnlyList<Action<World, Entity>> ComponentAdders
    );
}
