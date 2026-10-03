using Yaeger.Platform;

namespace Yaeger.Tests.Platform;

public class TextureSamplingTests
{
    [Fact]
    public void Default_ShouldBeLinearClampWithoutMipmaps()
    {
        var s = TextureSampling.Default;

        Assert.Equal(TextureFilter.Linear, s.Filter);
        Assert.Equal(TextureWrap.Clamp, s.Wrap);
        Assert.False(s.UsesMipmaps);
    }

    [Fact]
    public void Tiled_ShouldUseMipmapsAndRepeat()
    {
        Assert.True(TextureSampling.Tiled.UsesMipmaps);
        Assert.Equal(TextureWrap.Repeat, TextureSampling.Tiled.Wrap);
    }

    [Fact]
    public void Table_ShouldReturnOverrideThenFallBackToDefault()
    {
        var table = new TextureSamplingTable();
        table.Set("a.png", TextureSampling.Pixelated);

        Assert.Equal(TextureSampling.Pixelated, table.Get("a.png"));
        Assert.Equal(TextureSampling.Default, table.Get("b.png"));
    }

    [Fact]
    public void Table_ClearShouldRevertToDefault()
    {
        var table = new TextureSamplingTable();
        table.Set("a.png", TextureSampling.Pixelated);

        Assert.True(table.Clear("a.png"));
        Assert.Equal(TextureSampling.Default, table.Get("a.png"));
    }

    [Fact]
    public void Table_ChangingDefaultShouldAffectUnoverriddenPaths()
    {
        var table = new TextureSamplingTable { Default = TextureSampling.Tiled };

        Assert.Equal(TextureSampling.Tiled, table.Get("x.png"));
    }
}
