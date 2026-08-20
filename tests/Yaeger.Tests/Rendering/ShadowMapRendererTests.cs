using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Exercises the GL-free light-space projection maths. Constructing a ShadowMapRenderer needs a live
// OpenGL context, so only the static matrix builder is unit-tested (mirroring the renderer/window
// test convention in CLAUDE.md).
public class ShadowMapRendererTests
{
    private static Vector3 ProjectToNdc(Vector3 worldPoint, Matrix4x4 lightSpace)
    {
        // Matches the GL convention used throughout the engine: clip = worldPoint(row) * matrix,
        // which Vector4.Transform computes directly.
        var clip = Vector4.Transform(new Vector4(worldPoint, 1f), lightSpace);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

    [Fact]
    public void ComputeLightSpaceMatrix_CentersSceneCenterInNdc()
    {
        var light = new DirectionalLight
        {
            Direction = Vector3.Normalize(new Vector3(0.3f, 1f, 0.2f)),
            Color = Color.White,
            Intensity = 1f,
        };
        var center = new Vector3(1f, 2f, -3f);

        var matrix = ShadowMapRenderer.ComputeLightSpaceMatrix(
            light,
            center,
            ShadowSettings.Default
        );
        var ndc = ProjectToNdc(center, matrix);

        Assert.Equal(0f, ndc.X, 3);
        Assert.Equal(0f, ndc.Y, 3);
    }

    [Fact]
    public void ComputeLightSpaceMatrix_PointTowardLightHasSmallerDepth()
    {
        var light = new DirectionalLight
        {
            Direction = Vector3.UnitY,
            Color = Color.White,
            Intensity = 1f,
        };
        var center = Vector3.Zero;
        var settings = ShadowSettings.Default;

        var matrix = ShadowMapRenderer.ComputeLightSpaceMatrix(light, center, settings);

        var centerDepth = ProjectToNdc(center, matrix).Z;
        // A point shifted toward the light sits nearer the light's eye, so its depth must be smaller.
        var nearerDepth = ProjectToNdc(center + Vector3.UnitY * 2f, matrix).Z;

        Assert.True(nearerDepth < centerDepth);
    }

    [Fact]
    public void ComputeLightSpaceMatrix_SceneCenterDepthWithinUnitRange()
    {
        var light = new DirectionalLight
        {
            Direction = Vector3.UnitY,
            Color = Color.White,
            Intensity = 1f,
        };

        var matrix = ShadowMapRenderer.ComputeLightSpaceMatrix(
            light,
            Vector3.Zero,
            ShadowSettings.Default
        );
        var depth = ProjectToNdc(Vector3.Zero, matrix).Z;

        Assert.InRange(depth, 0f, 1f);
    }

    // ── Up vector: continuity through the zenith ─────────────────────────────

    private static float MaxElementDelta(Matrix4x4 a, Matrix4x4 b)
    {
        var max = 0f;
        max = MathF.Max(max, MathF.Abs(a.M11 - b.M11));
        max = MathF.Max(max, MathF.Abs(a.M12 - b.M12));
        max = MathF.Max(max, MathF.Abs(a.M13 - b.M13));
        max = MathF.Max(max, MathF.Abs(a.M21 - b.M21));
        max = MathF.Max(max, MathF.Abs(a.M22 - b.M22));
        max = MathF.Max(max, MathF.Abs(a.M23 - b.M23));
        max = MathF.Max(max, MathF.Abs(a.M31 - b.M31));
        max = MathF.Max(max, MathF.Abs(a.M32 - b.M32));
        max = MathF.Max(max, MathF.Abs(a.M33 - b.M33));
        return max;
    }

    [Fact]
    public void UpVector_AwayFromVertical_IsWorldUp()
    {
        Assert.Equal(Vector3.UnitY, ShadowMapRenderer.UpVector(Vector3.UnitX));
        Assert.Equal(
            Vector3.UnitY,
            ShadowMapRenderer.UpVector(Vector3.Normalize(new Vector3(1f, 0.5f, 0f)))
        );
    }

    [Fact]
    public void UpVector_AtVertical_IsNotParallelToTheLight()
    {
        var up = ShadowMapRenderer.UpVector(Vector3.UnitY);

        Assert.Equal(Vector3.UnitZ, up);
        Assert.True(MathF.Abs(Vector3.Dot(up, Vector3.UnitY)) < 0.01f);
    }

    [Fact]
    public void UpVector_IsAlwaysUnitLengthAndNeverParallelToTheLight()
    {
        // Sweep a light from the horizon up through the zenith and down the other side.
        for (var step = 0; step <= 360; step++)
        {
            var angle = step / 360f * MathF.PI;
            var direction = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
            var up = ShadowMapRenderer.UpVector(direction);

            Assert.Equal(1f, up.Length(), precision: 4);
            Assert.True(
                MathF.Abs(Vector3.Dot(up, direction)) < 0.999f,
                $"up parallel to the light at step {step}"
            );
        }
    }

    [Fact]
    public void ComputeLightSpaceMatrix_SweepingThroughZenith_HasNoDiscontinuity()
    {
        // The regression this guards: picking the up vector with a hard threshold rotates the
        // shadow map ~90 degrees in the single frame the light crosses it, and every shadow in the
        // scene snaps with it. Stepping finely, no single step may change the basis abruptly.
        var settings = ShadowSettings.Default;
        var previous = default(Matrix4x4);
        var largest = 0f;

        for (var step = 0; step <= 400; step++)
        {
            // 60 to 120 degrees of elevation: straight through vertical at the midpoint.
            var angle = (60f + step / 400f * 60f) * MathF.PI / 180f;
            var light = new DirectionalLight
            {
                Direction = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f),
                Color = Color.White,
                Intensity = 1f,
            };

            var matrix = ShadowMapRenderer.ComputeLightSpaceMatrix(light, Vector3.Zero, settings);
            if (step > 0)
                largest = MathF.Max(largest, MaxElementDelta(previous, matrix));

            previous = matrix;
        }

        // Measured: ~0.003 per step with the blended up vector, ~0.1 with a hard threshold switch
        // (the raw ~90-degree basis flip, scaled down by the orthographic projection). 0.02 sits
        // clear of both.
        Assert.True(largest < 0.02f, $"light-space basis jumped by {largest} in a single step");
    }

    // ── Horizon fade ─────────────────────────────────────────────────────────

    [Fact]
    public void ComputeShadowStrength_HighLight_IsFullStrength()
    {
        var light = new DirectionalLight { Direction = Vector3.UnitY, Intensity = 1f };

        Assert.Equal(1f, ShadowMapRenderer.ComputeShadowStrength(light, ShadowSettings.Default));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    public void ComputeShadowStrength_AtOrBelowTheHorizon_IsZero(float elevation)
    {
        var light = new DirectionalLight
        {
            Direction = new Vector3(
                MathF.Sqrt(MathF.Max(1f - elevation * elevation, 0f)),
                elevation,
                0f
            ),
            Intensity = 1f,
        };

        Assert.Equal(0f, ShadowMapRenderer.ComputeShadowStrength(light, ShadowSettings.Default));
    }

    [Fact]
    public void ComputeShadowStrength_AcrossTheFadeBand_RampsMonotonically()
    {
        var settings = ShadowSettings.Default with { HorizonFadeElevation = 0.2f };
        var previous = 0f;

        for (var step = 0; step <= 40; step++)
        {
            var elevation = step / 40f * 0.25f;
            var light = new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(1f, elevation, 0f)),
                Intensity = 1f,
            };

            var strength = ShadowMapRenderer.ComputeShadowStrength(light, settings);
            Assert.InRange(strength, 0f, 1f);
            Assert.True(strength >= previous, $"strength dipped at step {step}");
            previous = strength;
        }

        Assert.Equal(1f, previous, precision: 3);
    }

    [Fact]
    public void ComputeShadowStrength_NoFadeBand_IsAHardCutAtTheHorizon()
    {
        var settings = ShadowSettings.Default with { HorizonFadeElevation = 0f };
        var justAbove = new DirectionalLight
        {
            Direction = Vector3.Normalize(new Vector3(1f, 0.001f, 0f)),
            Intensity = 1f,
        };

        Assert.Equal(1f, ShadowMapRenderer.ComputeShadowStrength(justAbove, settings));
    }

    // ── Auto-fit ─────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLightSpaceMatrix_AutoFit_FramesTheWholeBoundingSphere()
    {
        var settings = ShadowSettings.Default with { AutoFit = true, OrthographicSize = 1f };
        var center = new Vector3(5f, 0f, -2f);
        const float radius = 30f;

        // A radius far larger than the configured extent: without fitting, points near the sphere's
        // edge fall outside the map and their shadows vanish.
        foreach (
            var direction in new[] { Vector3.UnitY, Vector3.Normalize(new Vector3(1f, 0.2f, 0.3f)) }
        )
        {
            var light = new DirectionalLight { Direction = direction, Intensity = 1f };
            var matrix = ShadowMapRenderer.ComputeLightSpaceMatrix(light, center, radius, settings);

            foreach (
                var offset in new[]
                {
                    Vector3.UnitX,
                    Vector3.UnitY,
                    Vector3.UnitZ,
                    -Vector3.UnitX,
                    -Vector3.UnitY,
                    -Vector3.UnitZ,
                }
            )
            {
                var ndc = ProjectToNdc(center + offset * radius, matrix);

                Assert.InRange(ndc.X, -1.001f, 1.001f);
                Assert.InRange(ndc.Y, -1.001f, 1.001f);
                Assert.InRange(ndc.Z, -0.001f, 1.001f);
            }
        }
    }

    [Fact]
    public void ComputeLightSpaceMatrix_AutoFitWithoutBounds_FallsBackToTheConfiguredExtent()
    {
        var settings = ShadowSettings.Default with { AutoFit = true };
        var light = new DirectionalLight { Direction = Vector3.UnitY, Intensity = 1f };

        var fitted = ShadowMapRenderer.ComputeLightSpaceMatrix(light, Vector3.Zero, 0f, settings);
        var configured = ShadowMapRenderer.ComputeLightSpaceMatrix(
            light,
            Vector3.Zero,
            settings with
            {
                AutoFit = false,
            }
        );

        Assert.Equal(configured, fitted);
    }

    [Fact]
    public void ComputeLightSpaceMatrix_AutoFitOff_IgnoresTheRadius()
    {
        var settings = ShadowSettings.Default;
        var light = new DirectionalLight { Direction = Vector3.UnitY, Intensity = 1f };

        var withRadius = ShadowMapRenderer.ComputeLightSpaceMatrix(
            light,
            Vector3.Zero,
            50f,
            settings
        );
        var withoutRadius = ShadowMapRenderer.ComputeLightSpaceMatrix(
            light,
            Vector3.Zero,
            settings
        );

        Assert.Equal(withoutRadius, withRadius);
    }

    [Fact]
    public void ComputeLightSpaceMatrix_DegenerateDirection_ProducesFiniteMatrix()
    {
        var light = new DirectionalLight
        {
            Direction = Vector3.Zero,
            Color = Color.White,
            Intensity = 1f,
        };

        var matrix = ShadowMapRenderer.ComputeLightSpaceMatrix(
            light,
            Vector3.Zero,
            ShadowSettings.Default
        );

        Assert.True(float.IsFinite(matrix.M11));
        Assert.True(float.IsFinite(matrix.M44));
        Assert.NotEqual(default, matrix);
    }
}
