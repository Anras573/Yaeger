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
