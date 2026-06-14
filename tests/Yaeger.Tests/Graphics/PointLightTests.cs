using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class PointLightTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(PointLight).IsValueType);
    }

    [Fact]
    public void StructDefault_HasZeroFields()
    {
        var light = default(PointLight);

        Assert.Equal(default(Color), light.Color);
        Assert.Equal(0f, light.Intensity);
        Assert.Equal(0f, light.Range);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new PointLight
        {
            Color = Color.White,
            Intensity = 1f,
            Range = 10f,
        };
        var b = new PointLight
        {
            Color = Color.White,
            Intensity = 1f,
            Range = 10f,
        };

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentRange_NotEqual()
    {
        var a = new PointLight
        {
            Color = Color.White,
            Intensity = 1f,
            Range = 10f,
        };
        var b = a with { Range = 5f };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Default_HasSafeValues()
    {
        var light = PointLight.Default;

        Assert.Equal(Color.White, light.Color);
        Assert.Equal(1f, light.Intensity);
        Assert.Equal(10f, light.Range);
    }
}
