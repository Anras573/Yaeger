using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class LightFlickerTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(LightFlicker).IsValueType);
    }

    [Fact]
    public void StructDefault_HasZeroFields()
    {
        var flicker = default(LightFlicker);

        Assert.Equal(0f, flicker.BaseIntensity);
        Assert.Equal(0f, flicker.Amplitude);
        Assert.Equal(0f, flicker.Frequency);
        Assert.Equal(0f, flicker.Seed);
        Assert.Equal(0f, flicker.PositionJitter);
        Assert.Equal(0f, flicker.Elapsed);
    }

    [Fact]
    public void Default_IsAModerateFastFlickerWithNoPositionalJitter()
    {
        var flicker = LightFlicker.Default;

        Assert.True(flicker.BaseIntensity > 0f);
        Assert.True(flicker.Amplitude > 0f);
        Assert.True(flicker.Frequency > 0f);
        Assert.Equal(0f, flicker.PositionJitter);
        Assert.Equal(0f, flicker.Elapsed);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new LightFlicker
        {
            BaseIntensity = 1f,
            Amplitude = 0.3f,
            Frequency = 3f,
            Seed = 2f,
        };
        var b = new LightFlicker
        {
            BaseIntensity = 1f,
            Amplitude = 0.3f,
            Frequency = 3f,
            Seed = 2f,
        };

        Assert.Equal(a, b);
    }
}
