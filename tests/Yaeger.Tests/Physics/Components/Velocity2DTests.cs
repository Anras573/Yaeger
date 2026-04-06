using System.Numerics;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.Physics.Components;

public class Velocity2DTests
{
    [Fact]
    public void Constructor_WithVector2_ShouldSetLinearVelocity()
    {
        var velocity = new Velocity2D(new Vector2(3, 4));

        Assert.Equal(3, velocity.Linear.X);
        Assert.Equal(4, velocity.Linear.Y);
        Assert.Equal(0, velocity.Angular);
    }

    [Fact]
    public void Constructor_WithXY_ShouldSetLinearVelocity()
    {
        var velocity = new Velocity2D(5, -2);

        Assert.Equal(5, velocity.Linear.X);
        Assert.Equal(-2, velocity.Linear.Y);
        Assert.Equal(0, velocity.Angular);
    }

    [Fact]
    public void Constructor_WithAngular_ShouldSetAngularVelocity()
    {
        var velocity = new Velocity2D(new Vector2(1, 2), 3.14f);

        Assert.Equal(3.14f, velocity.Angular);
    }

    [Fact]
    public void Zero_ShouldReturnZeroVelocity()
    {
        var velocity = Velocity2D.Zero;

        Assert.Equal(Vector2.Zero, velocity.Linear);
        Assert.Equal(0, velocity.Angular);
    }

    [Fact]
    public void Velocity2D_ShouldBeValueType()
    {
        var v1 = new Velocity2D(1, 2);
        var v2 = v1;
        v2.Linear = new Vector2(10, 20);

        Assert.Equal(1, v1.Linear.X);
        Assert.Equal(10, v2.Linear.X);
    }
}
