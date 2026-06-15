using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class SpotLightTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(SpotLight).IsValueType);
    }

    [Fact]
    public void StructDefault_HasZeroFields()
    {
        var light = default(SpotLight);

        Assert.Equal(default(Color), light.Color);
        Assert.Equal(0f, light.Intensity);
        Assert.Equal(Vector3.Zero, light.Direction);
        Assert.Equal(0f, light.InnerConeAngle);
        Assert.Equal(0f, light.OuterConeAngle);
        Assert.Equal(0f, light.Range);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new SpotLight
        {
            Color = Color.White,
            Intensity = 1f,
            Direction = -Vector3.UnitY,
            InnerConeAngle = 0.3f,
            OuterConeAngle = 0.5f,
            Range = 10f,
        };
        var b = new SpotLight
        {
            Color = Color.White,
            Intensity = 1f,
            Direction = -Vector3.UnitY,
            InnerConeAngle = 0.3f,
            OuterConeAngle = 0.5f,
            Range = 10f,
        };

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentOuterConeAngle_NotEqual()
    {
        var a = SpotLight.Default;
        var b = a with { OuterConeAngle = a.OuterConeAngle + 0.1f };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Default_HasSafeValues()
    {
        var light = SpotLight.Default;

        Assert.Equal(Color.White, light.Color);
        Assert.Equal(1f, light.Intensity);
        Assert.Equal(-Vector3.UnitY, light.Direction);
        Assert.True(light.InnerConeAngle <= light.OuterConeAngle);
        Assert.Equal(10f, light.Range);
    }
}
