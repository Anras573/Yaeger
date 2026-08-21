using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class ProceduralSkyTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(ProceduralSky).IsValueType);
    }

    [Fact]
    public void StructDefault_HasZeroFields()
    {
        var sky = default(ProceduralSky);

        Assert.Equal(Vector3.Zero, sky.SunDirection);
        Assert.Equal(Vector3.Zero, sky.MoonDirection);
        Assert.Equal(0f, sky.DaylightFactor);
        Assert.Equal(Vector2.Zero, sky.CloudWind);
        Assert.Equal(0f, sky.CloudScale);
        Assert.Equal(0f, sky.CloudCoverage);
        Assert.Equal(0f, sky.StarDensity);
        Assert.Equal(0f, sky.MoonPhase);
        Assert.Equal(0f, sky.Elapsed);
    }

    [Fact]
    public void Default_IsAClearNoonSkyWithSunOverheadAndMoonOpposite()
    {
        var sky = ProceduralSky.Default;

        Assert.Equal(Vector3.UnitY, sky.SunDirection);
        Assert.Equal(-Vector3.UnitY, sky.MoonDirection);
        Assert.Equal(1f, sky.DaylightFactor);
        Assert.Equal(0.5f, sky.MoonPhase);
        Assert.Equal(0f, sky.Elapsed);
        Assert.InRange(sky.StarDensity, 0f, 1f);
        Assert.InRange(sky.CloudCoverage, 0f, 1f);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new ProceduralSky
        {
            SunDirection = Vector3.UnitY,
            CloudWind = new Vector2(0.1f, 0.2f),
            StarDensity = 0.9f,
        };
        var b = new ProceduralSky
        {
            SunDirection = Vector3.UnitY,
            CloudWind = new Vector2(0.1f, 0.2f),
            StarDensity = 0.9f,
        };

        Assert.Equal(a, b);
    }
}
