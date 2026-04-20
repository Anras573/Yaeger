namespace Yaeger.ECS;

/// <summary>
/// Fluent builder for constructing <see cref="Prefab"/> instances in code.
/// </summary>
/// <example>
/// <code>
/// var ballPrefab = new PrefabBuilder()
///     .With(new Sprite("Assets/ball.png"))
///     .With(new Transform2D(Vector2.Zero, 0f, new Vector2(0.025f)))
///     .Build();
///
/// var entity = world.Instantiate(ballPrefab);
/// </code>
/// </example>
public sealed class PrefabBuilder
{
    private readonly List<Action<World, Entity>> _componentAdders = [];

    /// <summary>
    /// Adds a component value to the prefab template.
    /// </summary>
    /// <typeparam name="T">The component type. Must be a value type (struct).</typeparam>
    /// <param name="component">The component value to store in the prefab.</param>
    /// <returns>This builder, for method chaining.</returns>
    public PrefabBuilder With<T>(T component)
        where T : struct
    {
        _componentAdders.Add((world, entity) => world.AddComponent(entity, component));
        return this;
    }

    /// <summary>
    /// Adds a raw component-adder action.
    /// Used internally by <see cref="PrefabLoader"/> when deserializing JSON prefabs.
    /// </summary>
    internal PrefabBuilder WithAction(Action<World, Entity> adder)
    {
        _componentAdders.Add(adder);
        return this;
    }

    /// <summary>
    /// Builds and returns the <see cref="Prefab"/>.
    /// </summary>
    public Prefab Build() => new(_componentAdders.ToList());
}
