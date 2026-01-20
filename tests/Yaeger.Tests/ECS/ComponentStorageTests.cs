using Yaeger.ECS;

namespace Yaeger.Tests.ECS;

public class ComponentStorageTests
{
    [Fact]
    public void Add_ShouldStoreComponent()
    {
        // Arrange
        var storage = new ComponentStorage<TestComponent>();
        var world = new World();
        var entity = world.CreateEntity();
        var component = new TestComponent { Value = 42 };

        // Act
        storage.Add(entity, component);

        // Assert
        Assert.True(storage.TryGet(entity, out var retrievedComponent));
        Assert.Equal(42, retrievedComponent.Value);
    }

    [Fact]
    public void TryGet_ShouldReturnFalseForNonExistentEntity()
    {
        // Arrange
        var storage = new ComponentStorage<TestComponent>();
        var world = new World();
        var entity = world.CreateEntity();

        // Act
        var result = storage.TryGet(entity, out var component);

        // Assert
        Assert.False(result);
        Assert.Equal(default, component);
    }

    [Fact]
    public void Remove_ShouldRemoveComponent()
    {
        // Arrange
        var storage = new ComponentStorage<TestComponent>();
        var world = new World();
        var entity = world.CreateEntity();
        storage.Add(entity, new TestComponent { Value = 42 });

        // Act
        var removed = storage.Remove(entity);

        // Assert
        Assert.True(removed);
        Assert.False(storage.TryGet(entity, out _));
    }

    [Fact]
    public void Remove_ShouldReturnFalseForNonExistentEntity()
    {
        // Arrange
        var storage = new ComponentStorage<TestComponent>();
        var world = new World();
        var entity = world.CreateEntity();

        // Act
        var removed = storage.Remove(entity);

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void Add_ShouldUpdateExistingComponent()
    {
        // Arrange
        var storage = new ComponentStorage<TestComponent>();
        var world = new World();
        var entity = world.CreateEntity();
        storage.Add(entity, new TestComponent { Value = 42 });

        // Act
        storage.Add(entity, new TestComponent { Value = 100 });

        // Assert
        Assert.True(storage.TryGet(entity, out var component));
        Assert.Equal(100, component.Value);
    }

    [Fact]
    public void All_ShouldReturnAllComponents()
    {
        // Arrange
        var storage = new ComponentStorage<TestComponent>();
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();
        storage.Add(entity1, new TestComponent { Value = 1 });
        storage.Add(entity2, new TestComponent { Value = 2 });

        // Act
        var components = storage.All().ToList();

        // Assert
        Assert.Equal(2, components.Count);
        Assert.Contains(components, kvp => kvp.Key.Equals(entity1) && kvp.Value.Value == 1);
        Assert.Contains(components, kvp => kvp.Key.Equals(entity2) && kvp.Value.Value == 2);
    }

    [Fact]
    public void All_ShouldReturnEmptyWhenNoComponents()
    {
        // Arrange
        var storage = new ComponentStorage<TestComponent>();

        // Act
        var components = storage.All().ToList();

        // Assert
        Assert.Empty(components);
    }

    [Fact]
    public void Storage_ShouldHandleMultipleEntities()
    {
        // Arrange
        var storage = new ComponentStorage<TestComponent>();
        var world = new World();
        var entities = Enumerable.Range(0, 10).Select(_ => world.CreateEntity()).ToList();

        // Act
        for (int i = 0; i < entities.Count; i++)
        {
            storage.Add(entities[i], new TestComponent { Value = i });
        }

        // Assert
        for (int i = 0; i < entities.Count; i++)
        {
            Assert.True(storage.TryGet(entities[i], out var component));
            Assert.Equal(i, component.Value);
        }
    }

    // Helper test component
    private struct TestComponent
    {
        public int Value;
    }
}