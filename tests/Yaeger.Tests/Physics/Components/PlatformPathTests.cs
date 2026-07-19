using System.Numerics;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.Physics.Components;

public class PlatformPathTests
{
    [Fact]
    public void Constructor_ShouldSetWaypointsSpeedAndPingPong()
    {
        Vector2[] waypoints = [new(0, 0), new(10, 0), new(10, 5)];

        var path = new PlatformPath(waypoints, speed: 2.5f, pingPong: true);

        Assert.Equal(waypoints, path.Waypoints);
        Assert.Equal(2.5f, path.Speed);
        Assert.True(path.PingPong);
    }

    [Fact]
    public void Constructor_ShouldDefaultToLooping()
    {
        Vector2[] waypoints = [new(0, 0), new(10, 0)];

        var path = new PlatformPath(waypoints, speed: 1f);

        Assert.False(path.PingPong);
    }

    [Fact]
    public void Constructor_ShouldStartHeadingTowardsSecondWaypoint()
    {
        Vector2[] waypoints = [new(0, 0), new(10, 0), new(10, 5)];

        var path = new PlatformPath(waypoints, speed: 1f);

        Assert.Equal(1, path.CurrentWaypointIndex);
        Assert.True(path.MovingForward);
    }

    [Fact]
    public void Constructor_WithFewerThanTwoWaypoints_ShouldThrow()
    {
        Vector2[] waypoints = [new(0, 0)];

        Assert.Throws<ArgumentException>(() => new PlatformPath(waypoints, speed: 1f));
    }

    [Fact]
    public void Constructor_WithNoWaypoints_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new PlatformPath([], speed: 1f));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_InvalidSpeed_ShouldThrow(float speed)
    {
        Vector2[] waypoints = [new(0, 0), new(10, 0)];

        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformPath(waypoints, speed));
    }
}
