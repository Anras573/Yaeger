using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side procedural-sky math — no GL context needed, like BillboardMathTests.
public class ProceduralSkyMathTests
{
    [Fact]
    public void StarRotation_SunAtQuarterTurnAngle_RotatesByThatAngle()
    {
        // A sun direction with horizontal = (cos, sin) at angle pi/2 (pointing along +Z).
        var sun = new Vector3(0f, 0.3f, 1f);

        var rotation = ProceduralSkyMath.StarRotation(sun);
        var rotated = Vector3.Transform(Vector3.UnitX, rotation);

        // atan2(1, 0) = pi/2; Matrix4x4.CreateRotationY(pi/2) sends +X to -Z.
        Assert.Equal(0f, rotated.X, precision: 4);
        Assert.Equal(-1f, rotated.Z, precision: 4);
    }

    [Fact]
    public void StarRotation_SunDueNorth_IsIdentity()
    {
        var sun = new Vector3(1f, 0.5f, 0f);

        var rotation = ProceduralSkyMath.StarRotation(sun);

        Assert.Equal(Matrix4x4.Identity, rotation);
    }

    [Fact]
    public void StarRotation_ContinuousAcrossTheAngleWraparound()
    {
        // X negative, Z crossing zero from either side: atan2 wraps from just under +pi to just
        // over -pi here, but the resulting rotation must still be continuous (no visible pop as
        // the sun crosses due south).
        var justBefore = ProceduralSkyMath.StarRotation(new Vector3(-1f, 0.2f, 0.001f));
        var justAfter = ProceduralSkyMath.StarRotation(new Vector3(-1f, 0.2f, -0.001f));

        var a = Vector3.Transform(Vector3.UnitX, justBefore);
        var b = Vector3.Transform(Vector3.UnitX, justAfter);

        Assert.True(Vector3.Distance(a, b) < 0.01f);
    }

    [Fact]
    public void StarRotation_NonFiniteSunDirection_DoesNotProduceNaN()
    {
        var rotation = ProceduralSkyMath.StarRotation(new Vector3(float.NaN, 1f, float.NaN));

        Assert.True(float.IsFinite(rotation.M11));
        Assert.True(float.IsFinite(rotation.M13));
        Assert.True(float.IsFinite(rotation.M31));
        Assert.True(float.IsFinite(rotation.M33));
    }

    [Fact]
    public void CloudScrollOffset_ScalesLinearlyWithElapsedTime()
    {
        var wind = new Vector2(0.1f, -0.2f);

        var offset = ProceduralSkyMath.CloudScrollOffset(wind, 10f);

        Assert.Equal(1f, offset.X, precision: 4);
        Assert.Equal(-2f, offset.Y, precision: 4);
    }

    [Fact]
    public void CloudScrollOffset_ZeroElapsed_IsZero()
    {
        var offset = ProceduralSkyMath.CloudScrollOffset(new Vector2(5f, 5f), 0f);

        Assert.Equal(Vector2.Zero, offset);
    }

    [Fact]
    public void CloudScrollOffset_ZeroWind_NeverMoves()
    {
        var offset = ProceduralSkyMath.CloudScrollOffset(Vector2.Zero, 1000f);

        Assert.Equal(Vector2.Zero, offset);
    }

    [Fact]
    public void CloudScrollOffset_NonFiniteInputs_SanitizeToZeroContribution()
    {
        var offset = ProceduralSkyMath.CloudScrollOffset(
            new Vector2(float.NaN, float.PositiveInfinity),
            10f
        );

        Assert.Equal(0f, offset.X);
        Assert.Equal(0f, offset.Y);
    }

    [Fact]
    public void CloudScrollOffset_NonFiniteElapsed_SanitizesToZero()
    {
        var offset = ProceduralSkyMath.CloudScrollOffset(new Vector2(1f, 1f), float.NaN);

        Assert.Equal(Vector2.Zero, offset);
    }

    [Fact]
    public void CloudScrollOffset_IsDeterministic()
    {
        var wind = new Vector2(0.05f, 0.03f);

        var a = ProceduralSkyMath.CloudScrollOffset(wind, 42f);
        var b = ProceduralSkyMath.CloudScrollOffset(wind, 42f);

        Assert.Equal(a, b);
    }
}
