using Yaeger.ECS;

namespace Yaeger.Tests.ECS;

public class WorldTests
{
    [Fact]
    public void CreateEntity_ShouldReturnUniqueEntity()
    {
        // Arrange
        var world = new World();

        // Act
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        // Assert
        Assert.NotEqual(entity1.Id, entity2.Id);
    }

    [Fact]
    public void CreateEntity_ShouldIncrementEntityId()
    {
        // Arrange
        var world = new World();

        // Act
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        // Assert
        Assert.Equal(entity1.Id + 1, entity2.Id);
    }

    [Fact]
    public void AddComponent_ShouldStoreComponentForEntity()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        var testComponent = new TestComponent { Value = 42 };

        // Act
        world.AddComponent(entity, testComponent);

        // Assert
        Assert.True(world.TryGetComponent<TestComponent>(entity, out var retrievedComponent));
        Assert.Equal(42, retrievedComponent.Value);
    }

    [Fact]
    public void TryGetComponent_ShouldReturnFalseForNonExistentComponent()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();

        // Act
        var result = world.TryGetComponent<TestComponent>(entity, out var component);

        // Assert
        Assert.False(result);
        Assert.Equal(default, component);
    }

    [Fact]
    public void RemoveComponent_ShouldRemoveComponentFromEntity()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        var testComponent = new TestComponent { Value = 42 };
        world.AddComponent(entity, testComponent);

        // Act
        var removed = world.RemoveComponent<TestComponent>(entity);

        // Assert
        Assert.True(removed);
        Assert.False(world.TryGetComponent<TestComponent>(entity, out _));
    }

    [Fact]
    public void RemoveComponent_ShouldReturnFalseForNonExistentComponent()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();

        // Act
        var removed = world.RemoveComponent<TestComponent>(entity);

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void DestroyEntity_ShouldRemoveEntityFromEntitiesList()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();

        // Act
        world.DestroyEntity(entity);

        // Assert
        Assert.DoesNotContain(entity, world.Entities);
    }

    [Fact]
    public void Entities_ShouldReturnAllCreatedEntities()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();
        var entity3 = world.CreateEntity();

        // Act
        var entities = world.Entities.ToList();

        // Assert
        Assert.Equal(3, entities.Count);
        Assert.Contains(entity1, entities);
        Assert.Contains(entity2, entities);
        Assert.Contains(entity3, entities);
    }

    [Fact]
    public void GetStore_ShouldReturnSameStoreForSameComponentType()
    {
        // Arrange
        var world = new World();

        // Act
        var store1 = world.GetStore<TestComponent>();
        var store2 = world.GetStore<TestComponent>();

        // Assert
        Assert.Same(store1, store2);
    }

    [Fact]
    public void AddComponent_ShouldUpdateExistingComponent()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 42 });

        // Act
        world.AddComponent(entity, new TestComponent { Value = 100 });

        // Assert
        Assert.True(world.TryGetComponent<TestComponent>(entity, out var component));
        Assert.Equal(100, component.Value);
    }

    [Fact]
    public void DestroyEntity_ShouldNotAffectOtherEntities()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        // Act
        world.DestroyEntity(entity1);

        // Assert
        Assert.DoesNotContain(entity1, world.Entities);
        Assert.Contains(entity2, world.Entities);
    }

    // Helper test component
    private struct TestComponent
    {
        public int Value;
    }
}