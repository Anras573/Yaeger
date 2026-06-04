using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class DirectionalLightTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(DirectionalLight).IsValueType);
    }

    [Fact]
    public void StructDefault_HasZeroFields()
    {
        var light = default(DirectionalLight);

        Assert.Equal(Vector3.Zero, light.Direction);
        Assert.Equal(default(Color), light.Color);
        Assert.Equal(0f, light.Intensity);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new DirectionalLight
        {
            Direction = new Vector3(0f, -1f, 0f),
            Color = new Color(255, 255, 255),
            Intensity = 1f,
        };
        var b = new DirectionalLight
        {
            Direction = new Vector3(0f, -1f, 0f),
            Color = new Color(255, 255, 255),
            Intensity = 1f,
        };

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentIntensity_NotEqual()
    {
        var a = new DirectionalLight
        {
            Direction = Vector3.UnitY,
            Color = Color.White,
            Intensity = 1f,
        };
        var b = a with { Intensity = 0.5f };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Default_HasSafeValues()
    {
        var light = DirectionalLight.Default;

        Assert.Equal(Vector3.UnitY, light.Direction);
        Assert.Equal(Color.White, light.Color);
        Assert.Equal(1f, light.Intensity);
    }
}
