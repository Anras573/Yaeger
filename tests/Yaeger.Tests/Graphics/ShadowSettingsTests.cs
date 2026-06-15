using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class ShadowSettingsTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(ShadowSettings).IsValueType);
    }

    [Fact]
    public void Default_HasSensibleValues()
    {
        var settings = ShadowSettings.Default;

        Assert.Equal(2048, settings.MapResolution);
        Assert.Equal(10f, settings.OrthographicSize);
        Assert.Equal(0.1f, settings.NearPlane);
        Assert.Equal(50f, settings.FarPlane);
        Assert.Equal(0.005f, settings.Bias);
        Assert.True(settings.EnablePcf);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = ShadowSettings.Default;
        var b = ShadowSettings.Default;

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentResolution_NotEqual()
    {
        var a = ShadowSettings.Default;
        var b = a with { MapResolution = 1024 };

        Assert.NotEqual(a, b);
    }
}
