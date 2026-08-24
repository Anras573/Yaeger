using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class PathFollow3DTests
{
    [Fact]
    public void Constructor_ShouldSetWaypointsSpeedAndDefaults()
    {
        Vector3[] waypoints = [new(0, 0, 0), new(10, 0, 0), new(10, 0, 5)];

        var path = new PathFollow3D(waypoints, speed: 2.5f);

        Assert.Equal(waypoints, path.Waypoints);
        Assert.Equal(2.5f, path.Speed);
        Assert.Equal(TweenLoopMode.Loop, path.LoopMode);
        Assert.True(path.YawOnly);
        Assert.Equal(Vector3.UnitY, path.Up);
    }

    [Fact]
    public void Constructor_ShouldDefaultTurnRateToOneFullTurnPerSecond()
    {
        var path = new PathFollow3D([new Vector3(0, 0, 0), new Vector3(1, 0, 0)], speed: 1f);

        Assert.Equal(MathF.Tau, path.TurnRate);
    }

    [Fact]
    public void Constructor_ShouldStartHeadingTowardsSecondWaypoint()
    {
        Vector3[] waypoints = [new(0, 0, 0), new(10, 0, 0), new(10, 0, 5)];

        var path = new PathFollow3D(waypoints, speed: 1f);

        Assert.Equal(1, path.CurrentWaypointIndex);
        Assert.True(path.MovingForward);
        Assert.False(path.IsFinished);
        Assert.Equal(0f, path.CurrentSpeed);
    }

    [Fact]
    public void Constructor_WithFewerThanTwoWaypoints_ShouldNotThrow()
    {
        var zero = new PathFollow3D([], speed: 1f);
        var one = new PathFollow3D([new Vector3(1, 2, 3)], speed: 1f);

        Assert.Empty(zero.Waypoints);
        Assert.Equal(0, zero.CurrentWaypointIndex);
        Assert.Single(one.Waypoints);
        Assert.Equal(0, one.CurrentWaypointIndex);
    }

    [Fact]
    public void Constructor_WithNullWaypoints_ShouldTreatAsEmpty()
    {
        var path = new PathFollow3D(null!, speed: 1f);

        Assert.Empty(path.Waypoints);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_InvalidSpeed_ShouldThrow(float speed)
    {
        Vector3[] waypoints = [new(0, 0, 0), new(1, 0, 0)];

        Assert.Throws<ArgumentOutOfRangeException>(() => new PathFollow3D(waypoints, speed));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_InvalidTurnRate_ShouldThrow(float turnRate)
    {
        Vector3[] waypoints = [new(0, 0, 0), new(1, 0, 0)];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PathFollow3D(waypoints, speed: 1f, turnRate: turnRate)
        );
    }

    [Fact]
    public void Constructor_ZeroTurnRate_ShouldBeAccepted()
    {
        var path = new PathFollow3D(
            [new Vector3(0, 0, 0), new Vector3(1, 0, 0)],
            speed: 1f,
            turnRate: 0f
        );

        Assert.Equal(0f, path.TurnRate);
    }

    [Fact]
    public void Constructor_WithNullUp_ShouldDefaultToUnitY()
    {
        var path = new PathFollow3D(
            [new Vector3(0, 0, 0), new Vector3(1, 0, 0)],
            speed: 1f,
            up: null
        );

        Assert.Equal(Vector3.UnitY, path.Up);
    }

    [Fact]
    public void Constructor_WithZeroUp_ShouldDefaultToUnitY()
    {
        var path = new PathFollow3D(
            [new Vector3(0, 0, 0), new Vector3(1, 0, 0)],
            speed: 1f,
            up: Vector3.Zero
        );

        Assert.Equal(Vector3.UnitY, path.Up);
    }

    [Fact]
    public void Constructor_WithUnnormalizedUp_ShouldNormalize()
    {
        var path = new PathFollow3D(
            [new Vector3(0, 0, 0), new Vector3(1, 0, 0)],
            speed: 1f,
            up: new Vector3(0, 5, 0)
        );

        Assert.Equal(Vector3.UnitY, path.Up);
    }
}
