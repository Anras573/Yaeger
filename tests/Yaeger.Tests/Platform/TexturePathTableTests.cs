using Yaeger.Platform;

namespace Yaeger.Tests.Platform;

public class TexturePathTableTests
{
    [Fact]
    public void GetId_ShouldReturnSameIdForEqualPathsWithDifferentReferences()
    {
        var table = new TexturePathTable();

        var a = table.GetId(new string("a.png".AsSpan()));
        var b = table.GetId("b.png");
        var a2 = table.GetId(new string("a.png".AsSpan()));

        Assert.Equal(a, a2);
        Assert.NotEqual(a, b);
        Assert.Equal("b.png", table.GetPath(b));
        Assert.Equal(2, table.Count);
    }
}
