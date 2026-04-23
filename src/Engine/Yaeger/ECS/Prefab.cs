namespace Yaeger.ECS;

/// <summary>
/// A reusable entity template that can be instantiated into a <see cref="World"/>.
/// A prefab captures a fixed set of component values that are applied to a new entity
/// on each call to <see cref="World.Instantiate"/>.
/// </summary>
/// <remarks>
/// Create prefabs with <see cref="PrefabBuilder"/>.
/// Because C# components are structs, each instantiation receives its own copy of
/// every component value stored in the prefab. Reference-typed fields inside
/// components are still copied by reference, so instantiated entities will share the
/// same referenced object unless the component itself provides immutable data or
/// performs its own defensive copy. Consumers should not mutate such shared
/// referenced data after building a prefab unless the component explicitly documents
/// that doing so is safe.
/// </remarks>
public sealed class Prefab
{
    private readonly List<Action<World, Entity>> _componentAdders;

    internal Prefab(List<Action<World, Entity>> componentAdders)
    {
        _componentAdders = componentAdders;
    }

    internal void Apply(World world, Entity entity)
    {
        foreach (var adder in _componentAdders)
        {
            adder(world, entity);
        }
    }
}
