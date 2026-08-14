namespace Yaeger.ECS;

/// <summary>
/// A handle to one <see cref="Scene"/> load, returned by <see cref="World.LoadScene"/>. Carries
/// exactly the entities that load created, so <see cref="Unload"/> can tear them down again
/// without the caller keeping its own list — and, since scene loading is additive, several
/// instances (from the same <see cref="Scene"/> or different ones) coexist independently: calling
/// <see cref="Unload"/> on one never touches another's entities.
/// </summary>
/// <remarks>
/// <see cref="Unload"/> destroys precisely the entities this instance's load created via plain
/// <see cref="World.DestroyEntity"/> — <em>not</em> <see cref="World.DestroyHierarchy"/> — because
/// a scene's own <c>Parent</c>-linked children are already part of this instance's entity list
/// (<see cref="Scene.Apply"/> creates every entity the file describes, parents and children
/// alike). Walking the hierarchy instead would risk destroying another instance's entities too,
/// if something outside this scene got parented onto one of its entities after load.
/// </remarks>
public sealed class SceneInstance
{
    private readonly World _world;

    internal SceneInstance(World world, IReadOnlyList<Entity> entities)
    {
        _world = world;
        Entities = entities;
    }

    /// <summary>The entities this instance's <see cref="World.LoadScene"/> call created, in scene-file order.</summary>
    public IReadOnlyList<Entity> Entities { get; }

    /// <summary><c>true</c> once <see cref="Unload"/> has run.</summary>
    public bool IsUnloaded { get; private set; }

    /// <summary>
    /// Destroys every entity this instance owns. Idempotent — calling it again on an
    /// already-unloaded instance is a silent no-op, so callers don't need to track whether they
    /// already unloaded a given instance.
    /// </summary>
    public void Unload()
    {
        if (IsUnloaded)
            return;

        foreach (var entity in Entities)
            _world.DestroyEntity(entity);

        IsUnloaded = true;
    }
}
