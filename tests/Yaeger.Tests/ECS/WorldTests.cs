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

    [Fact]
    public void DestroyEntity_ShouldRemoveComponentsFromEntity()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 42 });

        // Act
        world.DestroyEntity(entity);

        // Assert
        Assert.DoesNotContain(entity, world.Entities);
        Assert.False(world.TryGetComponent<TestComponent>(entity, out _));
    }

    // ── DestroyHierarchy (issue #191) ───────────────────────────────────────

    [Fact]
    public void DestroyHierarchy_EntityWithNoChildren_DestroysOnlyItself()
    {
        var world = new World();
        var lonely = world.CreateEntity();
        var other = world.CreateEntity();

        world.DestroyHierarchy(lonely);

        Assert.DoesNotContain(lonely, world.Entities);
        Assert.Contains(other, world.Entities);
    }

    [Fact]
    public void DestroyHierarchy_SingleLevelChildren_DestroysParentAndAllChildren()
    {
        var world = new World();
        var parent = world.CreateEntity();
        var childA = world.CreateEntity();
        var childB = world.CreateEntity();
        world.AddComponent(childA, new Parent(parent));
        world.AddComponent(childB, new Parent(parent));

        world.DestroyHierarchy(parent);

        Assert.DoesNotContain(parent, world.Entities);
        Assert.DoesNotContain(childA, world.Entities);
        Assert.DoesNotContain(childB, world.Entities);
    }

    [Fact]
    public void DestroyHierarchy_MultiLevelTree_DestroysEveryDescendant()
    {
        var world = new World();
        var grandparent = world.CreateEntity();
        var parent = world.CreateEntity();
        var child = world.CreateEntity();
        var grandchild = world.CreateEntity();
        world.AddComponent(parent, new Parent(grandparent));
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(grandchild, new Parent(child));

        world.DestroyHierarchy(grandparent);

        Assert.DoesNotContain(grandparent, world.Entities);
        Assert.DoesNotContain(parent, world.Entities);
        Assert.DoesNotContain(child, world.Entities);
        Assert.DoesNotContain(grandchild, world.Entities);
    }

    [Fact]
    public void DestroyHierarchy_DoesNotTouchEntitiesOutsideTheSubtree()
    {
        var world = new World();
        var parent = world.CreateEntity();
        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));

        var unrelatedParent = world.CreateEntity();
        var unrelatedChild = world.CreateEntity();
        world.AddComponent(unrelatedChild, new Parent(unrelatedParent));

        world.DestroyHierarchy(parent);

        Assert.DoesNotContain(parent, world.Entities);
        Assert.DoesNotContain(child, world.Entities);
        Assert.Contains(unrelatedParent, world.Entities);
        Assert.Contains(unrelatedChild, world.Entities);
    }

    [Fact]
    public void DestroyHierarchy_RemovesTagRegistrationsForEveryDestroyedEntity()
    {
        var world = new World();
        var parent = world.CreateEntity("root");
        var child = world.CreateEntity("leaf");
        world.AddComponent(child, new Parent(parent));

        world.DestroyHierarchy(parent);

        Assert.False(world.TryGetEntity("root", out _));
        Assert.False(world.TryGetEntity("leaf", out _));
    }

    [Fact]
    public void DestroyHierarchy_DestroyingAMiddleNode_LeavesItsAncestorsAlone()
    {
        var world = new World();
        var grandparent = world.CreateEntity();
        var parent = world.CreateEntity();
        var child = world.CreateEntity();
        world.AddComponent(parent, new Parent(grandparent));
        world.AddComponent(child, new Parent(parent));

        // Destroying a middle node cascades downward only — ancestors are untouched, matching
        // DestroyEntity's existing orphan-to-world-space contract for whatever's above the call.
        world.DestroyHierarchy(parent);

        Assert.Contains(grandparent, world.Entities);
        Assert.DoesNotContain(parent, world.Entities);
        Assert.DoesNotContain(child, world.Entities);
    }

    [Fact]
    public void DestroyHierarchy_CyclicParentChain_ThrowsInvalidOperationException()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        world.AddComponent(a, new Parent(b));
        world.AddComponent(b, new Parent(a));

        Assert.Throws<InvalidOperationException>(() => world.DestroyHierarchy(a));
    }

    [Fact]
    public void DestroyHierarchy_SelfParentedEntity_ThrowsInvalidOperationException()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Parent(entity));

        Assert.Throws<InvalidOperationException>(() => world.DestroyHierarchy(entity));
    }

    // Helper test component
    private struct TestComponent
    {
        public int Value;
    }
}
