using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class FogSettingsTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(FogSettings).IsValueType);
    }

    [Fact]
    public void StructDefault_HasZeroFields()
    {
        var fog = default(FogSettings);

        Assert.Equal(default(Color), fog.Color);
        Assert.Equal(FogMode.ExponentialSquared, fog.Mode);
        Assert.Equal(0f, fog.Density);
        Assert.Equal(0f, fog.Start);
        Assert.Equal(0f, fog.End);
    }

    [Fact]
    public void Default_IsExponentialSquaredWithPositiveRange()
    {
        var fog = FogSettings.Default;

        Assert.Equal(Color.White, fog.Color);
        Assert.Equal(FogMode.ExponentialSquared, fog.Mode);
        Assert.True(fog.Density > 0f);
        Assert.True(fog.End > fog.Start);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new FogSettings
        {
            Color = Color.White,
            Mode = FogMode.Linear,
            Start = 10f,
            End = 50f,
        };
        var b = new FogSettings
        {
            Color = Color.White,
            Mode = FogMode.Linear,
            Start = 10f,
            End = 50f,
        };

        Assert.Equal(a, b);
    }
}
