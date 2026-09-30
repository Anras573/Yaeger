using Silk.NET.Input;
using Yaeger.Input;

namespace Yaeger.Tests.Input;

public class KeyMapperTests
{
    [Theory]
    [InlineData(Key.Number0, Keys.Num0)]
    [InlineData(Key.Number1, Keys.Num1)]
    [InlineData(Key.Number2, Keys.Num2)]
    [InlineData(Key.Number3, Keys.Num3)]
    [InlineData(Key.Number4, Keys.Num4)]
    [InlineData(Key.Number5, Keys.Num5)]
    [InlineData(Key.Number6, Keys.Num6)]
    [InlineData(Key.Number7, Keys.Num7)]
    [InlineData(Key.Number8, Keys.Num8)]
    [InlineData(Key.Number9, Keys.Num9)]
    public void TryGetMappedKey_ShouldMapNumberRow(Key key, Keys expected)
    {
        var mapped = KeyMapper.TryGetMappedKey(key, out var result);

        Assert.True(mapped);
        Assert.Equal(expected, result);
    }
}
