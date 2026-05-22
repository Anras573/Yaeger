using Yaeger.ECS;

namespace Yaeger.Tests.ECS;

public class WorldExtensionsTests
{
    [Fact]
    public void Query_WithTwoComponents_ShouldReturnEntitiesWithBothComponents()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();
        var entity3 = world.CreateEntity();

        world.AddComponent(entity1, new ComponentA { Value = 1 });
        world.AddComponent(entity1, new ComponentB { Name = "A" });

        world.AddComponent(entity2, new ComponentA { Value = 2 });
        world.AddComponent(entity2, new ComponentB { Name = "B" });

        world.AddComponent(entity3, new ComponentA { Value = 3 });
        // entity3 does not have ComponentB

        // Act
        var results = world.Query<ComponentA, ComponentB>().ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(
            results,
            r => r.Item1.Equals(entity1) && r.Item2.Value == 1 && r.Item3.Name == "A"
        );
        Assert.Contains(
            results,
            r => r.Item1.Equals(entity2) && r.Item2.Value == 2 && r.Item3.Name == "B"
        );
    }

    [Fact]
    public void Query_WithTwoComponents_ShouldReturnEmptyWhenNoMatches()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new ComponentA { Value = 1 });

        // Act
        var results = world.Query<ComponentA, ComponentB>().ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Query_WithThreeComponents_ShouldReturnEntitiesWithAllComponents()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        world.AddComponent(entity1, new ComponentA { Value = 1 });
        world.AddComponent(entity1, new ComponentB { Name = "A" });
        world.AddComponent(entity1, new ComponentC { Flag = true });

        world.AddComponent(entity2, new ComponentA { Value = 2 });
        world.AddComponent(entity2, new ComponentB { Name = "B" });
        // entity2 does not have ComponentC

        // Act
        var results = world.Query<ComponentA, ComponentB, ComponentC>().ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(
            results,
            r =>
                r.Item1.Equals(entity1) && r.Item2.Value == 1 && r.Item3.Name == "A" && r.Item4.Flag
        );
    }

    [Fact]
    public void Query_WithFourComponents_ShouldReturnEntitiesWithAllComponents()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        world.AddComponent(entity1, new ComponentA { Value = 1 });
        world.AddComponent(entity1, new ComponentB { Name = "A" });
        world.AddComponent(entity1, new ComponentC { Flag = true });
        world.AddComponent(entity1, new ComponentD { Score = 100 });

        world.AddComponent(entity2, new ComponentA { Value = 2 });
        world.AddComponent(entity2, new ComponentB { Name = "B" });
        world.AddComponent(entity2, new ComponentC { Flag = false });
        // entity2 does not have ComponentD

        // Act
        var results = world.Query<ComponentA, ComponentB, ComponentC, ComponentD>().ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(
            results,
            r =>
                r.Item1.Equals(entity1)
                && r.Item2.Value == 1
                && r.Item3.Name == "A"
                && r.Item4.Flag
                && r.Item5.Score == 100
        );
    }

    [Fact]
    public void Query_ShouldNotReturnDuplicates()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new ComponentA { Value = 1 });
        world.AddComponent(entity, new ComponentB { Name = "A" });

        // Act
        var results = world.Query<ComponentA, ComponentB>().ToList();

        // Assert
        Assert.Single(results);
    }

    [Fact]
    public void Query_ShouldWorkAfterComponentRemoval()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new ComponentA { Value = 1 });
        world.AddComponent(entity, new ComponentB { Name = "A" });

        // Act - Remove ComponentB
        world.RemoveComponent<ComponentB>(entity);
        var results = world.Query<ComponentA, ComponentB>().ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Query_ShouldWorkWithMultipleEntities()
    {
        // Arrange
        var world = new World();
        var entities = Enumerable.Range(0, 5).Select(_ => world.CreateEntity()).ToList();

        foreach (var entity in entities)
        {
            world.AddComponent(entity, new ComponentA { Value = entity.Id });
            world.AddComponent(entity, new ComponentB { Name = $"Entity{entity.Id}" });
        }

        // Act
        var results = world.Query<ComponentA, ComponentB>().ToList();

        // Assert
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_WithTwoComponents_UsingOverride_ShouldReturnCorrectResults()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        world.AddComponent(entity1, new ComponentA { Value = 1 });
        world.AddComponent(entity1, new ComponentB { Name = "A" });

        world.AddComponent(entity2, new ComponentA { Value = 2 });
        world.AddComponent(entity2, new ComponentB { Name = "B" });

        // Act - Force iteration to start with ComponentB (index 1)
        var results = world.Query<ComponentA, ComponentB>(forceIndex: 1).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(
            results,
            r => r.Item1.Equals(entity1) && r.Item2.Value == 1 && r.Item3.Name == "A"
        );
    }

    [Fact]
    public void Query_WithTwoComponents_OverrideInvalidIndex_ShouldThrow()
    {
        // Arrange
        var world = new World();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.Query<ComponentA, ComponentB>(forceIndex: 2).ToList()
        );
    }

    [Fact]
    public void Query_WithThreeComponents_UsingOverride_ShouldReturnCorrectResults()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        world.AddComponent(entity1, new ComponentA { Value = 1 });
        world.AddComponent(entity1, new ComponentB { Name = "A" });
        world.AddComponent(entity1, new ComponentC { Flag = true });

        world.AddComponent(entity2, new ComponentA { Value = 2 });
        world.AddComponent(entity2, new ComponentB { Name = "B" });

        // Act - Force iteration to start with ComponentB (index 1)
        var results = world.Query<ComponentA, ComponentB, ComponentC>(forceIndex: 1).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(
            results,
            r =>
                r.Item1.Equals(entity1) && r.Item2.Value == 1 && r.Item3.Name == "A" && r.Item4.Flag
        );
    }

    [Fact]
    public void Query_WithThreeComponents_UsingOverrideForThirdComponent_ShouldReturnCorrectResults()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();

        world.AddComponent(entity, new ComponentA { Value = 1 });
        world.AddComponent(entity, new ComponentB { Name = "A" });
        world.AddComponent(entity, new ComponentC { Flag = true });

        // Act - Force iteration to start with ComponentC (index 2)
        var results = world.Query<ComponentA, ComponentB, ComponentC>(forceIndex: 2).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(
            results,
            r => r.Item1.Equals(entity) && r.Item2.Value == 1 && r.Item3.Name == "A" && r.Item4.Flag
        );
    }

    [Fact]
    public void Query_WithFourComponents_UsingOverride_ShouldReturnCorrectResults()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        world.AddComponent(entity1, new ComponentA { Value = 1 });
        world.AddComponent(entity1, new ComponentB { Name = "A" });
        world.AddComponent(entity1, new ComponentC { Flag = true });
        world.AddComponent(entity1, new ComponentD { Score = 100 });

        world.AddComponent(entity2, new ComponentA { Value = 2 });
        world.AddComponent(entity2, new ComponentB { Name = "B" });
        world.AddComponent(entity2, new ComponentC { Flag = false });

        // Act - Force iteration to start with ComponentD (index 3)
        var results = world
            .Query<ComponentA, ComponentB, ComponentC, ComponentD>(forceIndex: 3)
            .ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(
            results,
            r =>
                r.Item1.Equals(entity1)
                && r.Item2.Value == 1
                && r.Item3.Name == "A"
                && r.Item4.Flag
                && r.Item5.Score == 100
        );
    }

    [Fact]
    public void Query_WithThreeComponents_OverrideInvalidIndex_ShouldThrow()
    {
        // Arrange
        var world = new World();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.Query<ComponentA, ComponentB, ComponentC>(forceIndex: 3).ToList()
        );
    }

    [Fact]
    public void Query_WithFourComponents_OverrideInvalidIndex_ShouldThrow()
    {
        // Arrange
        var world = new World();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.Query<ComponentA, ComponentB, ComponentC, ComponentD>(forceIndex: 4).ToList()
        );
    }

    // Helper test components
    private struct ComponentA
    {
        public int Value;
    }

    private struct ComponentB
    {
        public string Name;
    }

    private struct ComponentC
    {
        public bool Flag;
    }

    private struct ComponentD
    {
        public int Score;
    }
}
