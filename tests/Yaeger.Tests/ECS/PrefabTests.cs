using Yaeger.ECS;

namespace Yaeger.Tests.ECS;

public class PrefabTests
{
    // ── PrefabBuilder ────────────────────────────────────────────────────────

    [Fact]
    public void PrefabBuilder_Build_ReturnsNonNullPrefab()
    {
        var prefab = new PrefabBuilder().Build();

        Assert.NotNull(prefab);
    }

    [Fact]
    public void PrefabBuilder_With_AllowsMethodChaining()
    {
        var prefab = new PrefabBuilder()
            .With(new ComponentA { Value = 1 })
            .With(new ComponentB { Name = "hello" })
            .Build();

        Assert.NotNull(prefab);
    }

    // ── World.Instantiate (no tag) ────────────────────────────────────────────

    [Fact]
    public void Instantiate_WithoutTag_CreatesEntityInWorld()
    {
        var world = new World();
        var prefab = new PrefabBuilder().Build();

        var entity = world.Instantiate(prefab);

        Assert.Contains(entity, world.Entities);
    }

    [Fact]
    public void Instantiate_WithoutTag_AppliesComponents()
    {
        var world = new World();
        var prefab = new PrefabBuilder().With(new ComponentA { Value = 42 }).Build();

        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<ComponentA>(entity, out var comp));
        Assert.Equal(42, comp.Value);
    }

    [Fact]
    public void Instantiate_WithoutTag_AppliesMultipleComponents()
    {
        var world = new World();
        var prefab = new PrefabBuilder()
            .With(new ComponentA { Value = 10 })
            .With(new ComponentB { Name = "test" })
            .Build();

        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<ComponentA>(entity, out var compA));
        Assert.True(world.TryGetComponent<ComponentB>(entity, out var compB));
        Assert.Equal(10, compA.Value);
        Assert.Equal("test", compB.Name);
    }

    // ── World.Instantiate (with tag) ──────────────────────────────────────────

    [Fact]
    public void Instantiate_WithTag_CreatesTaggedEntity()
    {
        var world = new World();
        var prefab = new PrefabBuilder().Build();

        var entity = world.Instantiate(prefab, "MyTag");

        Assert.True(world.TryGetEntity("MyTag", out var found));
        Assert.Equal(entity, found);
    }

    [Fact]
    public void Instantiate_WithTag_AppliesComponents()
    {
        var world = new World();
        var prefab = new PrefabBuilder().With(new ComponentA { Value = 7 }).Build();

        world.Instantiate(prefab, "Tagged");

        var entity = world.GetEntity("Tagged");
        Assert.True(world.TryGetComponent<ComponentA>(entity, out var comp));
        Assert.Equal(7, comp.Value);
    }

    // ── Multiple instantiations share no state ────────────────────────────────

    [Fact]
    public void Instantiate_TwiceFromSamePrefab_CreatesTwoDistinctEntities()
    {
        var world = new World();
        var prefab = new PrefabBuilder().With(new ComponentA { Value = 1 }).Build();

        var entity1 = world.Instantiate(prefab);
        var entity2 = world.Instantiate(prefab);

        Assert.NotEqual(entity1.Id, entity2.Id);
    }

    [Fact]
    public void Instantiate_TwiceFromSamePrefab_EachEntityHasItsOwnComponentCopy()
    {
        var world = new World();
        var prefab = new PrefabBuilder().With(new ComponentA { Value = 5 }).Build();

        var entity1 = world.Instantiate(prefab);
        var entity2 = world.Instantiate(prefab);

        // Mutate entity1's component value.
        world.AddComponent(entity1, new ComponentA { Value = 99 });

        Assert.True(world.TryGetComponent<ComponentA>(entity2, out var comp2));
        Assert.Equal(5, comp2.Value);
    }

    // ── Post-instantiation overrides ──────────────────────────────────────────

    [Fact]
    public void Instantiate_AllowsPostInstantiationOverride()
    {
        var world = new World();
        var prefab = new PrefabBuilder().With(new ComponentA { Value = 1 }).Build();

        var entity = world.Instantiate(prefab);
        world.AddComponent(entity, new ComponentA { Value = 100 });

        Assert.True(world.TryGetComponent<ComponentA>(entity, out var comp));
        Assert.Equal(100, comp.Value);
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public void Instantiate_NullPrefab_ThrowsArgumentNullException()
    {
        var world = new World();

        Assert.Throws<ArgumentNullException>(() => world.Instantiate(null!));
    }

    // ── Helper test components ─────────────────────────────────────────────────

    private struct ComponentA
    {
        public int Value;
    }

    private struct ComponentB
    {
        public string Name;
    }
}
