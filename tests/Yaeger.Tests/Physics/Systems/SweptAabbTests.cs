using System.Numerics;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class SweptAabbTests
{
    [Fact]
    public void TryGetEntryFraction_HeadOnAlongX_ShouldReturnEntryFraction()
    {
        // Arrange — moving box travels from x=0 to x=10; obstacle's near (Minkowski-expanded)
        // face sits at x=9.45, so contact happens at t = 9.45/10 = 0.945.
        var start = new Vector2(0, 0);
        var halfSize = new Vector2(0.5f, 0.5f);
        var displacement = new Vector2(10, 0);
        var obstacleCenter = new Vector2(5, 0);
        var obstacleHalfSize = new Vector2(0.5f, 0.5f);

        // Act
        var hit = SweptAabb.TryGetEntryFraction(
            start,
            halfSize,
            displacement,
            obstacleCenter,
            obstacleHalfSize,
            out var tEntry
        );

        // Assert
        Assert.True(hit);
        Assert.Equal(0.4f, tEntry, 0.0001f);
    }

    [Fact]
    public void TryGetEntryFraction_DiagonalApproach_ShouldReturnEntryFraction()
    {
        // Arrange — moving diagonally into a box positioned diagonally ahead.
        var start = new Vector2(0, 0);
        var halfSize = new Vector2(0.5f, 0.5f);
        var displacement = new Vector2(10, 10);
        var obstacleCenter = new Vector2(5, 5);
        var obstacleHalfSize = new Vector2(0.5f, 0.5f);

        // Act
        var hit = SweptAabb.TryGetEntryFraction(
            start,
            halfSize,
            displacement,
            obstacleCenter,
            obstacleHalfSize,
            out var tEntry
        );

        // Assert
        Assert.True(hit);
        Assert.Equal(0.4f, tEntry, 0.0001f);
    }

    [Fact]
    public void TryGetEntryFraction_DisplacementTooShortToReachObstacle_ShouldReturnFalse()
    {
        // Arrange — the obstacle is far beyond where this displacement ends.
        var start = new Vector2(0, 0);
        var halfSize = new Vector2(0.5f, 0.5f);
        var displacement = new Vector2(1, 0);
        var obstacleCenter = new Vector2(10, 0);
        var obstacleHalfSize = new Vector2(0.5f, 0.5f);

        // Act
        var hit = SweptAabb.TryGetEntryFraction(
            start,
            halfSize,
            displacement,
            obstacleCenter,
            obstacleHalfSize,
            out _
        );

        // Assert
        Assert.False(hit);
    }

    [Fact]
    public void TryGetEntryFraction_AlreadyOverlappingAtStart_ShouldReturnFalse()
    {
        // Arrange — the moving box already overlaps the obstacle at t=0; that's an existing
        // overlap for discrete detection to handle, not a tunneling case for this sweep.
        var start = new Vector2(5, 0);
        var halfSize = new Vector2(0.5f, 0.5f);
        var displacement = new Vector2(1, 0);
        var obstacleCenter = new Vector2(5, 0);
        var obstacleHalfSize = new Vector2(0.5f, 0.5f);

        // Act
        var hit = SweptAabb.TryGetEntryFraction(
            start,
            halfSize,
            displacement,
            obstacleCenter,
            obstacleHalfSize,
            out _
        );

        // Assert
        Assert.False(hit);
    }

    [Fact]
    public void TryGetEntryFraction_ParallelAndOutsideOtherAxisSlab_ShouldReturnFalse()
    {
        // Arrange — moving purely along X at a Y far outside the obstacle's vertical extent:
        // a permanent miss regardless of how far the X displacement travels.
        var start = new Vector2(0, 10);
        var halfSize = new Vector2(0.5f, 0.5f);
        var displacement = new Vector2(5, 0);
        var obstacleCenter = new Vector2(2, 0);
        var obstacleHalfSize = new Vector2(0.5f, 0.5f);

        // Act
        var hit = SweptAabb.TryGetEntryFraction(
            start,
            halfSize,
            displacement,
            obstacleCenter,
            obstacleHalfSize,
            out _
        );

        // Assert
        Assert.False(hit);
    }

    [Fact]
    public void TryGetEntryFraction_NoVerticalOverlapAlongPath_ShouldReturnFalse()
    {
        // Arrange — the X sweep alone would suggest a hit, but the obstacle sits at a Y the
        // moving box never occupies (a near-miss the naive per-axis test alone must reject).
        var start = new Vector2(0, 0);
        var halfSize = new Vector2(0.5f, 0.5f);
        var displacement = new Vector2(10, 0);
        var obstacleCenter = new Vector2(5, 5);
        var obstacleHalfSize = new Vector2(0.5f, 0.5f);

        // Act
        var hit = SweptAabb.TryGetEntryFraction(
            start,
            halfSize,
            displacement,
            obstacleCenter,
            obstacleHalfSize,
            out _
        );

        // Assert
        Assert.False(hit);
    }

    [Fact]
    public void TryGetEntryFraction_ZeroDisplacement_ShouldReturnFalse()
    {
        // Arrange — no movement at all; nothing for a sweep to find regardless of geometry.
        var start = new Vector2(0, 0);
        var halfSize = new Vector2(0.5f, 0.5f);
        var displacement = Vector2.Zero;
        var obstacleCenter = new Vector2(5, 0);
        var obstacleHalfSize = new Vector2(0.5f, 0.5f);

        // Act
        var hit = SweptAabb.TryGetEntryFraction(
            start,
            halfSize,
            displacement,
            obstacleCenter,
            obstacleHalfSize,
            out _
        );

        // Assert
        Assert.False(hit);
    }
}
