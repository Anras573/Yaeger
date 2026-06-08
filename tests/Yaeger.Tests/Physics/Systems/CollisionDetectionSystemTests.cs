using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class CollisionDetectionSystemTests
{
    #region Box vs Box

    [Fact]
    public void Detect_BoxBox_ShouldDetectOverlap()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert
        Assert.Single(system.Manifolds);
        var manifold = system.Manifolds[0];
        var entities = new[] { manifold.EntityA, manifold.EntityB };
        Assert.Contains(a, entities);
        Assert.Contains(b, entities);
        Assert.NotEqual(manifold.EntityA, manifold.EntityB);
        Assert.True(manifold.PenetrationDepth > 0);
    }

    [Fact]
    public void Detect_BoxBox_ShouldNotDetectNonOverlapping()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(5, 0)));
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert
        Assert.Empty(system.Manifolds);
    }

    [Fact]
    public void Detect_BoxBox_ShouldCalculateCorrectNormal_XAxis()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1.5f, 0)));
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — overlap is less on X axis, so normal should be along X
        Assert.Single(system.Manifolds);
        var manifold = system.Manifolds[0];
        Assert.Equal(1.0f, manifold.Normal.X);
        Assert.Equal(0.0f, manifold.Normal.Y);
        Assert.Equal(0.5f, manifold.PenetrationDepth, 0.001f);
    }

    [Fact]
    public void Detect_BoxBox_ShouldCalculateCorrectNormal_YAxis()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(0, 1.5f)));
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — overlap is less on Y axis, so normal should be along Y
        Assert.Single(system.Manifolds);
        var manifold = system.Manifolds[0];
        Assert.Equal(0.0f, manifold.Normal.X);
        Assert.Equal(1.0f, manifold.Normal.Y);
        Assert.Equal(0.5f, manifold.PenetrationDepth, 0.001f);
    }

    [Fact]
    public void Detect_BoxBox_ShouldRespectColliderOffset()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new BoxCollider2D(new Vector2(2, 2), new Vector2(5, 0)));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(6, 0)));
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — A is at (0,0) but offset by (5,0), so effective center is (5,0)
        // B is at (6,0) — they should overlap
        Assert.Single(system.Manifolds);
    }

    #endregion

    #region Circle vs Circle

    [Fact]
    public void Detect_CircleCircle_ShouldDetectOverlap()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new CircleCollider2D(1.0f));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1.5f, 0)));
        world.AddComponent(b, new CircleCollider2D(1.0f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert
        Assert.Single(system.Manifolds);
        var manifold = system.Manifolds[0];
        Assert.Equal(0.5f, manifold.PenetrationDepth, 0.001f);
    }

    [Fact]
    public void Detect_CircleCircle_ShouldNotDetectNonOverlapping()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new CircleCollider2D(1.0f));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(5, 0)));
        world.AddComponent(b, new CircleCollider2D(1.0f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert
        Assert.Empty(system.Manifolds);
    }

    [Fact]
    public void Detect_CircleCircle_ShouldCalculateCorrectNormal()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new CircleCollider2D(1.0f));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1.0f, 0)));
        world.AddComponent(b, new CircleCollider2D(1.0f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — normal should point from A to B (along positive X)
        Assert.Single(system.Manifolds);
        var manifold = system.Manifolds[0];
        Assert.Equal(1.0f, manifold.Normal.X, 0.001f);
        Assert.Equal(0.0f, manifold.Normal.Y, 0.001f);
    }

    #endregion

    #region Box vs Circle

    [Fact]
    public void Detect_BoxCircle_ShouldDetectOverlap()
    {
        // Arrange
        var world = new World();

        var box = world.CreateEntity();
        world.AddComponent(box, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(box, new BoxCollider2D(2, 2));

        var circle = world.CreateEntity();
        world.AddComponent(circle, new Transform2D(new Vector2(1.5f, 0)));
        world.AddComponent(circle, new CircleCollider2D(1.0f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert
        Assert.Single(system.Manifolds);
        var manifold = system.Manifolds[0];
        var entities = new[] { manifold.EntityA, manifold.EntityB };
        Assert.Contains(box, entities);
        Assert.Contains(circle, entities);
        Assert.NotEqual(manifold.EntityA, manifold.EntityB);
        Assert.True(manifold.PenetrationDepth > 0);
    }

    [Fact]
    public void Detect_BoxCircle_ShouldNotDetectNonOverlapping()
    {
        // Arrange
        var world = new World();

        var box = world.CreateEntity();
        world.AddComponent(box, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(box, new BoxCollider2D(2, 2));

        var circle = world.CreateEntity();
        world.AddComponent(circle, new Transform2D(new Vector2(5, 0)));
        world.AddComponent(circle, new CircleCollider2D(1.0f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert
        Assert.Empty(system.Manifolds);
    }

    [Fact]
    public void Detect_BoxCircle_CircleInsideBox_ShouldDetect()
    {
        // Arrange
        var world = new World();

        var box = world.CreateEntity();
        world.AddComponent(box, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(box, new BoxCollider2D(10, 10));

        var circle = world.CreateEntity();
        world.AddComponent(circle, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(circle, new CircleCollider2D(0.5f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert
        Assert.Single(system.Manifolds);
    }

    [Fact]
    public void Detect_BoxCircle_CircleInsideBox_ContactPointShouldBeOnBoxSurface()
    {
        // Arrange — circle centered inside box, slightly offset on X
        var world = new World();

        var box = world.CreateEntity();
        world.AddComponent(box, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(box, new BoxCollider2D(10, 10)); // half size = 5

        var circle = world.CreateEntity();
        world.AddComponent(circle, new Transform2D(new Vector2(2, 0)));
        world.AddComponent(circle, new CircleCollider2D(0.5f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — contact point should be on the box face, not at circle center
        Assert.Single(system.Manifolds);
        var manifold = system.Manifolds[0];

        // Circle is at (2,0), closest point on box is (2,0) (inside), distance=0
        // Shortest push-out axis is X (dx=3 < dy=5), normal = (1,0)
        // Contact point should be on the right face of the box: (5, 0)
        Assert.Equal(5.0f, manifold.ContactPoint.X, 0.001f);
        Assert.Equal(0.0f, manifold.ContactPoint.Y, 0.001f);
    }

    #endregion

    [Fact]
    public void Detect_ShouldClearManifoldsBetweenCalls()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();
        Assert.Single(system.Manifolds);

        // Move B far away
        world.AddComponent(b, new Transform2D(new Vector2(100, 0)));
        system.Detect();

        // Assert — should be empty now
        Assert.Empty(system.Manifolds);
    }

    [Fact]
    public void Detect_MultipleCollisions_ShouldReportAll()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var c = world.CreateEntity();
        world.AddComponent(c, new Transform2D(new Vector2(-1, 0)));
        world.AddComponent(c, new BoxCollider2D(2, 2));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — A overlaps B (overlap 1), A overlaps C (overlap 1)
        // B and C are exactly touching (overlap = 0), so no collision
        Assert.Equal(2, system.Manifolds.Count);
    }

    [Fact]
    public void Detect_BoxCircle_SameEntity_ShouldNotSelfCollide()
    {
        // Arrange — entity has both BoxCollider2D and CircleCollider2D
        var world = new World();

        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(entity, new BoxCollider2D(2, 2));
        world.AddComponent(entity, new CircleCollider2D(1.0f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — should not generate a self-collision manifold
        Assert.Empty(system.Manifolds);
    }

    #region Broadphase

    [Fact]
    public void Detect_Broadphase_FarApartEntitiesProduceNoManifolds()
    {
        // Arrange — 4 boxes in separate regions of a 10x10 world, none touching
        var world = new World();

        var positions = new[]
        {
            new Vector2(-4f, -4f),
            new Vector2(4f, -4f),
            new Vector2(-4f, 4f),
            new Vector2(4f, 4f),
        };

        foreach (var pos in positions)
        {
            var e = world.CreateEntity();
            world.AddComponent(e, new Transform2D(pos));
            world.AddComponent(e, new BoxCollider2D(1f, 1f));
        }

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — none of the four boxes are close enough to collide
        Assert.Empty(system.Manifolds);
    }

    [Fact]
    public void Detect_Broadphase_OnlyAdjacentCirclesCollide()
    {
        // Arrange — 3 circles in a row; only the first two overlap
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0f, 0f)));
        world.AddComponent(a, new CircleCollider2D(0.1f));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(0.15f, 0f)));
        world.AddComponent(b, new CircleCollider2D(0.1f));

        var c = world.CreateEntity();
        world.AddComponent(c, new Transform2D(new Vector2(5f, 0f)));
        world.AddComponent(c, new CircleCollider2D(0.1f));

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — only a and b share a spatial cell and overlap; c is pruned
        Assert.Single(system.Manifolds);
        var entities = new[] { system.Manifolds[0].EntityA, system.Manifolds[0].EntityB };
        Assert.Contains(a, entities);
        Assert.Contains(b, entities);
    }

    [Fact]
    public void Detect_Broadphase_ManyNonCollidingEntitiesProduceNoManifolds()
    {
        // Arrange — 100 circles spread across a grid, none overlapping
        var world = new World();

        for (var row = 0; row < 10; row++)
        {
            for (var col = 0; col < 10; col++)
            {
                var e = world.CreateEntity();
                world.AddComponent(e, new Transform2D(new Vector2(col * 0.5f, row * 0.5f)));
                world.AddComponent(e, new CircleCollider2D(0.1f));
            }
        }

        var system = new CollisionDetectionSystem(world);

        // Act
        system.Detect();

        // Assert — circles are spaced 0.5 apart with radius 0.1, so none touch
        Assert.Empty(system.Manifolds);
    }

    #endregion
}
