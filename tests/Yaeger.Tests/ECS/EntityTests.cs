using Yaeger.ECS;

namespace Yaeger.Tests.ECS;

public class EntityTests
{
    [Fact]
    public void Entity_ShouldHaveUniqueId()
    {
        // Arrange
        var world = new World();

        // Act
        var entity = world.CreateEntity();

        // Assert
        Assert.True(entity.Id > 0);
    }

    [Fact]
    public void Entity_ShouldBeValueType()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();

        // Act
        var entity2 = entity1;

        // Assert
        Assert.Equal(entity1.Id, entity2.Id);
        Assert.Equal(entity1, entity2);
    }

    [Fact]
    public void Entity_GetHashCode_ShouldReturnId()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();

        // Act
        var hashCode = entity.GetHashCode();

        // Assert
        Assert.Equal(entity.Id, hashCode);
    }

    [Fact]
    public void Entity_Equals_ShouldCompareByValue()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();

        // Act & Assert
        Assert.Equal(entity1, entity1);
        Assert.NotEqual(entity1, entity2);
    }

    [Fact]
    public void Entity_CanBeUsedInHashSet()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();
        var hashSet = new HashSet<Entity>();

        // Act
        hashSet.Add(entity1);
        hashSet.Add(entity2);
        hashSet.Add(entity1); // Duplicate

        // Assert
        Assert.Equal(2, hashSet.Count);
        Assert.Contains(entity1, hashSet);
        Assert.Contains(entity2, hashSet);
    }

    [Fact]
    public void Entity_CanBeUsedAsDictionaryKey()
    {
        // Arrange
        var world = new World();
        var entity1 = world.CreateEntity();
        var entity2 = world.CreateEntity();
        var dictionary = new Dictionary<Entity, int>();

        // Act
        dictionary[entity1] = 100;
        dictionary[entity2] = 200;

        // Assert
        Assert.Equal(100, dictionary[entity1]);
        Assert.Equal(200, dictionary[entity2]);
    }
}