using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class AmbientLightTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(AmbientLight).IsValueType);
    }

    [Fact]
    public void StructDefault_HasZeroFields()
    {
        var ambient = default(AmbientLight);

        Assert.Equal(default(Color), ambient.Color);
        Assert.Equal(0f, ambient.Intensity);
    }

    [Fact]
    public void Default_MatchesTheShaderConstantItReplaced()
    {
        // The PBR path used to hardcode `vec3(0.03) * albedo * ao`; white at 0.03 reproduces it, so
        // a scene that never attaches an AmbientLight renders exactly as it did before.
        var ambient = AmbientLight.Default;

        Assert.Equal(Color.White, ambient.Color);
        Assert.Equal(0.03f, ambient.Intensity, precision: 5);
    }

    [Fact]
    public void RecordStruct_EqualityByValue()
    {
        var a = new AmbientLight { Color = Color.White, Intensity = 0.2f };
        var b = new AmbientLight { Color = Color.White, Intensity = 0.2f };

        Assert.Equal(a, b);
    }
}
