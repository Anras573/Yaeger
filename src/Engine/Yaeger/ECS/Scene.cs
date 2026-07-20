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
    /// <remarks>
    /// <para>
    /// Tag collisions are handled by <see cref="World.CreateEntity(string)"/>'s existing
    /// rebind semantics: if a scene tag is already bound to an entity in the world, the tag
    /// is silently transferred to the newly created entity and the previous entity loses its
    /// reverse mapping. The scene loader doesn't try to prevent this — callers that need
    /// collision detection should check for existing tags before calling
    /// <c>world.Instantiate(scene)</c>.
    /// </para>
    /// <para>
    /// Application happens in two passes: every entity in the scene is created (and its tag
    /// registered) first, and only then are component adders run, entity by entity in scene-file
    /// order. This lets a component adder resolve a tag belonging to <i>any</i> entity in the
    /// same scene — including one that appears later in the file — via <see cref="World.GetEntity"/>/
    /// <see cref="World.TryGetEntity"/>, which is how <c>Parent</c> supports forward references.
    /// </para>
    /// </remarks>
    internal IReadOnlyList<Entity> Apply(World world)
    {
        var created = new List<Entity>(_entities.Count);
        foreach (var entry in _entities)
        {
            var entity = string.IsNullOrWhiteSpace(entry.Tag)
                ? world.CreateEntity()
                : world.CreateEntity(entry.Tag);
            created.Add(entity);
        }

        for (var i = 0; i < _entities.Count; i++)
        {
            foreach (var adder in _entities[i].ComponentAdders)
            {
                adder(world, created[i]);
            }
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
