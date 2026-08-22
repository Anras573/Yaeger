using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side re-bake decision math — no GL context needed, like ProceduralSkyMathTests.
public class ProceduralSkyIblTests
{
    // ── AngleBetweenDegrees ────────────────────────────────────────────────

    [Fact]
    public void AngleBetweenDegrees_SameDirection_IsZero()
    {
        var angle = ProceduralSkyIbl.AngleBetweenDegrees(Vector3.UnitY, Vector3.UnitY);

        Assert.Equal(0f, angle, precision: 3);
    }

    [Fact]
    public void AngleBetweenDegrees_OppositeDirections_Is180()
    {
        var angle = ProceduralSkyIbl.AngleBetweenDegrees(Vector3.UnitY, -Vector3.UnitY);

        Assert.Equal(180f, angle, precision: 3);
    }

    [Fact]
    public void AngleBetweenDegrees_PerpendicularDirections_Is90()
    {
        var angle = ProceduralSkyIbl.AngleBetweenDegrees(Vector3.UnitX, Vector3.UnitY);

        Assert.Equal(90f, angle, precision: 3);
    }

    [Fact]
    public void AngleBetweenDegrees_IsInsensitiveToMagnitude()
    {
        var angle = ProceduralSkyIbl.AngleBetweenDegrees(
            Vector3.UnitX * 5f,
            new Vector3(1f, 1f, 0f) * 0.01f
        );

        Assert.Equal(45f, angle, precision: 2);
    }

    [Fact]
    public void AngleBetweenDegrees_ZeroVector_ReturnsZeroInsteadOfNaN()
    {
        var angle = ProceduralSkyIbl.AngleBetweenDegrees(Vector3.Zero, Vector3.UnitY);

        Assert.Equal(0f, angle);
    }

    [Fact]
    public void AngleBetweenDegrees_NonFiniteInput_ReturnsZeroInsteadOfNaN()
    {
        var angle = ProceduralSkyIbl.AngleBetweenDegrees(
            new Vector3(float.NaN, 1f, 0f),
            Vector3.UnitY
        );

        Assert.Equal(0f, angle);
    }

    // ── ShouldRebake ──────────────────────────────────────────────────────

    [Fact]
    public void ShouldRebake_NoPreviousBake_IsAlwaysTrue()
    {
        var due = ProceduralSkyIbl.ShouldRebake(
            hasBaked: false,
            elapsedSinceLastBake: 0f,
            angleSinceLastBakeDegrees: 0f,
            ProceduralSkyIblSettings.Default
        );

        Assert.True(due);
    }

    [Fact]
    public void ShouldRebake_IntervalNotYetElapsed_IsFalseEvenIfSunMovedFar()
    {
        var settings = new ProceduralSkyIblSettings
        {
            MinRebakeInterval = 2f,
            SunDirectionThresholdDegrees = 3f,
        };

        var due = ProceduralSkyIbl.ShouldRebake(
            hasBaked: true,
            elapsedSinceLastBake: 0.5f,
            angleSinceLastBakeDegrees: 90f,
            settings
        );

        Assert.False(due);
    }

    [Fact]
    public void ShouldRebake_IntervalElapsedButSunBarelyMoved_IsFalse()
    {
        var settings = new ProceduralSkyIblSettings
        {
            MinRebakeInterval = 2f,
            SunDirectionThresholdDegrees = 3f,
        };

        var due = ProceduralSkyIbl.ShouldRebake(
            hasBaked: true,
            elapsedSinceLastBake: 10f,
            angleSinceLastBakeDegrees: 0.1f,
            settings
        );

        Assert.False(due);
    }

    [Fact]
    public void ShouldRebake_BothIntervalElapsedAndSunMoved_IsTrue()
    {
        var settings = new ProceduralSkyIblSettings
        {
            MinRebakeInterval = 2f,
            SunDirectionThresholdDegrees = 3f,
        };

        var due = ProceduralSkyIbl.ShouldRebake(
            hasBaked: true,
            elapsedSinceLastBake: 2f,
            angleSinceLastBakeDegrees: 3f,
            settings
        );

        Assert.True(due);
    }

    [Fact]
    public void ShouldRebake_ZeroThresholds_TriggersOnIntervalAlone()
    {
        var settings = new ProceduralSkyIblSettings
        {
            MinRebakeInterval = 1f,
            SunDirectionThresholdDegrees = 0f,
        };

        var due = ProceduralSkyIbl.ShouldRebake(
            hasBaked: true,
            elapsedSinceLastBake: 1f,
            angleSinceLastBakeDegrees: 0f,
            settings
        );

        Assert.True(due);
    }

    [Fact]
    public void ShouldRebake_ZeroInterval_TriggersAsSoonAsSunMoves()
    {
        var settings = new ProceduralSkyIblSettings
        {
            MinRebakeInterval = 0f,
            SunDirectionThresholdDegrees = 1f,
        };

        var due = ProceduralSkyIbl.ShouldRebake(
            hasBaked: true,
            elapsedSinceLastBake: 0f,
            angleSinceLastBakeDegrees: 1f,
            settings
        );

        Assert.True(due);
    }

    [Fact]
    public void ShouldRebake_NegativeSettings_AreTreatedAsZero()
    {
        var settings = new ProceduralSkyIblSettings
        {
            MinRebakeInterval = -5f,
            SunDirectionThresholdDegrees = -5f,
        };

        var due = ProceduralSkyIbl.ShouldRebake(
            hasBaked: true,
            elapsedSinceLastBake: 0f,
            angleSinceLastBakeDegrees: 0f,
            settings
        );

        Assert.True(due);
    }

    [Fact]
    public void ShouldRebake_NonFiniteElapsedOrAngle_IsTreatedAsZeroNotThrowing()
    {
        var settings = ProceduralSkyIblSettings.Default;

        var due = ProceduralSkyIbl.ShouldRebake(
            hasBaked: true,
            elapsedSinceLastBake: float.NaN,
            angleSinceLastBakeDegrees: float.PositiveInfinity,
            settings
        );

        // NaN elapsed sanitizes to 0 (never satisfies a positive interval); +Inf angle sanitizes to
        // 0 as well per AngleBetweenDegrees's own contract, so neither condition is met.
        Assert.False(due);
    }

    // ── A simulated long-running cycle stays bounded ────────────────────────

    [Fact]
    public void ShouldRebake_OverAFastCycle_BoundsRebakeCountByTheInterval()
    {
        // A sun completing a full 360-degree revolution every 10 simulated seconds, stepped at
        // 60 fps: without the interval acting as a hard floor, a 3-degree threshold alone would
        // fire roughly 120 times (360 / 3) over those 10 seconds — many times a second. The
        // interval must keep the actual rebake count far below that.
        var settings = new ProceduralSkyIblSettings
        {
            MinRebakeInterval = 2f,
            SunDirectionThresholdDegrees = 3f,
        };

        const float dayLength = 10f;
        const float deltaTime = 1f / 60f;
        const int steps = 600; // 10 simulated seconds

        var hasBaked = false;
        float elapsedSinceLastBake = 0f;
        var lastBakedAngle = 0f;
        var totalAngle = 0f;
        var rebakeCount = 0;

        for (var step = 0; step < steps; step++)
        {
            elapsedSinceLastBake += deltaTime;
            totalAngle += 360f * (deltaTime / dayLength);
            var angleSinceLastBake = totalAngle - lastBakedAngle;

            if (
                ProceduralSkyIbl.ShouldRebake(
                    hasBaked,
                    elapsedSinceLastBake,
                    angleSinceLastBake,
                    settings
                )
            )
            {
                rebakeCount++;
                hasBaked = true;
                elapsedSinceLastBake = 0f;
                lastBakedAngle = totalAngle;
            }
        }

        // At most one rebake per MinRebakeInterval, plus the first: 10s / 2s = 5, +1 for the
        // unconditional first bake.
        Assert.True(rebakeCount <= 6, $"Expected a bounded rebake count, got {rebakeCount}.");
        Assert.True(rebakeCount > 1);
    }
}
