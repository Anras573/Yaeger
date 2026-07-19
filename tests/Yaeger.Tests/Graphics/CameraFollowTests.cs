using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class CameraFollowTests
{
    [Fact]
    public void Constructor_ShouldDefaultToStandardValues()
    {
        var world = new World();
        var target = world.CreateEntity();

        var follow = new CameraFollow(target);

        Assert.Equal(target, follow.TargetEntity);
        Assert.Equal(5f, follow.Smoothing);
        Assert.Equal(Vector2.Zero, follow.DeadzoneHalfExtents);
        Assert.Equal(0f, follow.LookAheadTime);
    }

    [Fact]
    public void Constructor_ShouldSetCustomValues()
    {
        var world = new World();
        var target = world.CreateEntity();

        var follow = new CameraFollow(
            target,
            smoothing: 8f,
            deadzoneHalfExtents: new Vector2(1, 0.5f),
            lookAheadTime: 0.3f
        );

        Assert.Equal(8f, follow.Smoothing);
        Assert.Equal(new Vector2(1, 0.5f), follow.DeadzoneHalfExtents);
        Assert.Equal(0.3f, follow.LookAheadTime);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void Constructor_NonPositiveSmoothing_ShouldNotThrow(float smoothing)
    {
        // Non-positive smoothing is a valid "snap instantly" configuration, not an error.
        var world = new World();
        var target = world.CreateEntity();

        var follow = new CameraFollow(target, smoothing: smoothing);

        Assert.Equal(smoothing, follow.Smoothing);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_NonFiniteSmoothing_ShouldThrow(float smoothing)
    {
        var world = new World();
        var target = world.CreateEntity();

        Assert.Throws<ArgumentOutOfRangeException>(() => new CameraFollow(target, smoothing));
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, -1f)]
    public void Constructor_NegativeDeadzone_ShouldThrow(float x, float y)
    {
        var world = new World();
        var target = world.CreateEntity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CameraFollow(target, deadzoneHalfExtents: new Vector2(x, y))
        );
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_InvalidLookAheadTime_ShouldThrow(float lookAheadTime)
    {
        var world = new World();
        var target = world.CreateEntity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CameraFollow(target, lookAheadTime: lookAheadTime)
        );
    }

    [Fact]
    public void Constructor_ZeroLookAheadTime_ShouldNotThrow()
    {
        var world = new World();
        var target = world.CreateEntity();

        var follow = new CameraFollow(target, lookAheadTime: 0f);

        Assert.Equal(0f, follow.LookAheadTime);
    }
}
